﻿using AncoraMVVM.Base;
using AncoraMVVM.Base.Interfaces;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using Ocell.Commands;
using Ocell.Library;
using Ocell.Library.Twitter;
using Ocell.Localization;
using PropertyChanged;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TweetSharp;

namespace Ocell.Pages.Elements
{
    [ImplementPropertyChanged]
    public class TweetModel : ExtendedViewModelBase
    {
        public ApplicationBarMode AppBarMode { get; set; }
        public bool Completed { get; set; }
        public bool IsMuting { get; set; }
        public TwitterStatus Tweet { get; set; }
        public bool HasReplies { get; set; }
        public bool IsFavorited { get; set; }
        public bool HasImage { get; set; }
        public ObservableCollection<ITweeter> UsersWhoRetweeted { get; set; }
        public int RetweetCount { get; set; }
        public bool HasRetweets { get; set; }
        public string WhoRetweeted { get; set; }
        public string Avatar { get; set; }
        public string ReplyText { get; set; }

        public DelegateCommand DeleteTweet { get; set; }
        public DelegateCommand Share { get; set; }
        public DelegateCommand Quote { get; set; }
        public DelegateCommand Favorite { get; set; }
        public DelegateCommand SendTweet { get; set; }
        public DelegateCommand NavigateToAuthor { get; set; }

        public string ImageSource { get; set; }

        public SafeObservable<ITweetable> Replies { get; set; }

        public SafeObservable<string> Images { get; set; }

        public event EventHandler<EventArgs<ITweetable>> TweetSent;

        Uri ImageNavigationUri;

        private void SetAvatar()
        {
            if (Tweet.User != null && Tweet.User.ProfileImageUrl != null)
                Avatar = Tweet.User.ProfileImageUrl.Replace("_normal", "");
        }

        private void GetReplies()
        {
            var convService = new ConversationService(DataTransfer.CurrentAccount);
            convService.Finished += (sender, e) => Progress.IsLoading = false;
            convService.GetConversationForStatus(Tweet, (statuses, response) =>
            {
                if (statuses != null)
                {
                    var statuses_noRepeat = statuses.Cast<ITweetable>().Except(Replies).ToList();
                    foreach (var status in statuses_noRepeat)
                        Replies.Add(status);
                }
            });

        }

        private async void GetRetweets()
        {
            var service = ServiceDispatcher.GetCurrentService();

            if (service != null && Tweet != null)
            {
                var response = await service.RetweetsAsync(new RetweetsOptions { Id = Tweet.Id });

                var statuses = response.Content;

                if (response.RequestSucceeded && statuses.Any())
                {
                    HasRetweets = true;
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        foreach (var rt in statuses)
                            UsersWhoRetweeted.Add(rt.Author);
                    });
                }
            }
        }

        public TweetModel()
        {
            UsersWhoRetweeted = new ObservableCollection<ITweeter>();
            Replies = new SafeObservable<ITweetable>();
            Images = new SafeObservable<string>();

            AppBarMode = ApplicationBarMode.Default;

            if (DataTransfer.Status == null)
            {
                Notificator.ShowError(Localization.Resources.ErrorLoadingTweet);
                Navigator.GoBack();
                return;
            }

            SetRetweetedStatus();
            SetAvatar();
            SetupCommands();

            HasReplies = (Tweet.InReplyToStatusId != null);
            HasImage = (Tweet.Entities != null && Tweet.Entities.Media.Any());
            IsFavorited = !Tweet.IsFavorited;
            IsFavorited = Tweet.IsFavorited;
            RetweetCount = Tweet.RetweetCount;

            if (Tweet.User == null || Tweet.User.Name == null)
                FillUser();

            UsersWhoRetweeted.CollectionChanged += (s, e) => RetweetCount = UsersWhoRetweeted.Count;
        }

        private void SetRetweetedStatus()
        {
            if (DataTransfer.Status.RetweetedStatus != null)
            {
                Tweet = DataTransfer.Status.RetweetedStatus;
                WhoRetweeted = " " + String.Format(Localization.Resources.RetweetBy, DataTransfer.Status.Author.ScreenName);
                HasRetweets = true;
            }
            else
            {
                Tweet = DataTransfer.Status;
                WhoRetweeted = "";
            }
        }

        public override void OnLoad()
        {
            GetRetweets();
            GetReplies();
            SetImage();
        }

        private void SetupCommands()
        {
            DeleteTweet = new DelegateCommand(async (obj) =>
            {
                var user = Config.Accounts.Value.FirstOrDefault(item => item != null && item.ScreenName == Tweet.Author.ScreenName);

                var response = await ServiceDispatcher.GetService(user).DeleteTweetAsync(new DeleteTweetOptions { Id = Tweet.Id });
                if (response.RequestSucceeded)
                    Notificator.ShowMessage(Localization.Resources.TweetDeleted);
                else
                    Notificator.ShowError(Localization.Resources.ErrorDeletingTweet);
            }, (obj) => Tweet != null && Tweet.Author != null && Config.Accounts.Value.Any(item => item != null && item.ScreenName == Tweet.Author.ScreenName));


            Share = new DelegateCommand((obj) => Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                EmailComposeTask emailComposeTask = new EmailComposeTask();

                emailComposeTask.Subject = String.Format(Localization.Resources.TweetFrom, Tweet.Author.ScreenName);
                emailComposeTask.Body = "@" + Tweet.Author.ScreenName + ": " + Tweet.Text +
                    Environment.NewLine + Environment.NewLine + Tweet.CreatedDate.ToString();

                emailComposeTask.Show();
            }), obj => Tweet != null);

            Quote = new DelegateCommand((obj) =>
            {
                Navigator.MessageAndNavigate<NewTweetModel, NewTweetArgs>(new NewTweetArgs
                {
                    Text = String.Format("RT @{0}: {1}", Tweet.Author.ScreenName, Tweet.Text),
                    ReplyToId = Tweet.Id
                });
            },
            obj => Config.Accounts.Value.Any() && Tweet != null);

            Favorite = new DelegateCommand(async () =>
            {
                var favCmd = new FavoriteCommand();
                var result = await favCmd.ExecuteAsync(Tweet);

                if (result)
                {
                    IsFavorited = !IsFavorited;
                    Tweet.IsFavorited = IsFavorited;
                }
            }, () => Tweet != null && Config.Accounts.Value.Count > 0 && DataTransfer.CurrentAccount != null);

            Favorite.BindCanExecuteToProperty(this, "Tweet", "IsFavorited");

            SendTweet = new DelegateCommand(async (parameter) =>
            {
                Progress.IsLoading = true;
                Progress.Text = Resources.SendingTweet;
                var response = await ServiceDispatcher.GetCurrentService().SendTweetAsync(new SendTweetOptions { InReplyToStatusId = Tweet.Id, Status = ReplyText });

                Progress.IsLoading = false;
                Progress.Text = "";
                if (!response.RequestSucceeded)
                    Notificator.ShowError(response.Error != null ? response.Error.Message : Resources.UnknownValue);
                else if (TweetSent != null)
                    TweetSent(this, new EventArgs<ITweetable>(response.Content));

            });

            NavigateToAuthor = new DelegateCommand((param) =>
            {
                Navigator.MessageAndNavigate<UserModel, TargetUser>(new TargetUser { Username = Tweet.AuthorName, User = Tweet.Author as TwitterUser });
            }, p => Tweet != null && (Tweet.Author != null || !string.IsNullOrWhiteSpace(Tweet.AuthorName)));

            NavigateToAuthor.BindCanExecuteToProperty(this, "Tweet");
        }

        async void FillUser()
        {
            var response = await ServiceDispatcher.GetDefaultService().GetUserProfileForAsync(new GetUserProfileForOptions { ScreenName = Tweet.Author.ScreenName });

            var user = response.Content;

            if (!response.RequestSucceeded)
                Notificator.ShowError(Localization.Resources.ErrorGettingProfile);

            Tweet.User = user;
            SetAvatar();
        }

        public void ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Progress.IsLoading = false;
            Notificator.ShowError(Localization.Resources.ErrorDownloadingImage);
        }

        public void ImageOpened(object sender, RoutedEventArgs e)
        {
            Progress.IsLoading = false;
            Progress.Text = "";
        }

        public void ImageTapped(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Image img = sender as Image;

            if (img != null)
            {
                var url = img.Tag as string;
                if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        var task = new Microsoft.Phone.Tasks.WebBrowserTask { Uri = new Uri(url, UriKind.Absolute) };
                        task.Show();
                    });
                }
            }
        }

        void SetImage()
        {
            if (Tweet.Entities == null)
                return;

            if (Tweet.Entities.Media != null && Tweet.Entities.Media.Any())
            {
                var photo = Tweet.Entities.Media.First();
                Images.Add(photo.MediaUrl);

            }

            if (Tweet.Entities.Urls != null && Tweet.Entities.Urls.Any())
            {
                var parser = new MediaLinkParser();
                foreach (var i in Tweet.Entities.Urls)
                {
                    if (i.EntityType == TwitterEntityType.Url)
                    {
                        var url = i as TwitterUrl;
                        if (url != null && !string.IsNullOrWhiteSpace(url.ExpandedValue))
                        {
                            string photoUrl;
                            if (parser.TryGetMediaUrl(url.ExpandedValue, out photoUrl) && !Images.Contains(photoUrl))
                                Images.Add(photoUrl);
                        }
                    }
                }
            }

            if (Images.Count > 0)
            {
                HasImage = true;
                Progress.IsLoading = true;
                Progress.Text = Localization.Resources.DownloadingImage;
            }
        }

        public void ReplyBoxGotFocus()
        {
            ReplyText = ReplyAllCommand.GetReplied(Tweet);
        }
    }
}
