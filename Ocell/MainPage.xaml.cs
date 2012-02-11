﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Controls;
using Ocell.Controls;
using TweetSharp;
using System.Linq;


namespace Ocell
{
    public partial class MainPage : PhoneApplicationPage
    {
        private ObservableCollection<TwitterResource> pivots;
        private bool selectionChangeFired;
        private Dictionary<string, ExtendedListBox> Lists;

        #region Loaders
        // Constructora
        public MainPage()
        {
            InitializeComponent();

            pivots = new ObservableCollection<TwitterResource>();
            Lists = new Dictionary<string, ExtendedListBox>();

            this.Loaded += new RoutedEventHandler(SetUpPivots);
            pivots.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(pivots_CollectionChanged);
            MainPivot.SelectionChanged += new SelectionChangedEventHandler(LoadTweetsOnPivot);
            MainPivot.SelectionChanged += new SelectionChangedEventHandler(RefreshCurrentAccount);

            MainPivot.DataContext = pivots;
            MainPivot.ItemsSource = pivots;
        }

        void SetUpPivots(object sender, RoutedEventArgs e)
        {
            if (Config.Accounts.Count == 0)
            {
                ShowLoginMsg();
                return;
            }

            DataTransfer.CurrentAccount = Config.Accounts[0];
            UpdatePivotTitle();

            GetPivotsFromConf();
            
        }

        
        #endregion

        void LoadTweetsOnPivot(object sender, SelectionChangedEventArgs e)
        {
            if (MainPivot.SelectedItem == null)
                return;
            ExtendedListBox ListBox;
            TwitterResource Resource = (TwitterResource)MainPivot.SelectedItem;
            if (Lists.TryGetValue(Resource.String, out ListBox))
            {
                Dispatcher.BeginInvoke(() => pBar.IsVisible = true);
                ListBox.Loader.Load();
            }
        }

        void RefreshCurrentAccount(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                TwitterResource item = (TwitterResource)e.AddedItems[0];
                DataTransfer.CurrentAccount = item.User;
                UpdatePivotTitle();
            }
        }
        void BindPivots()
        {
            MainPivot.DataContext = null;
            MainPivot.ItemsSource = pivots;
        }

        void ShowLoginMsg()
        {
            Dispatcher.BeginInvoke(() =>
            {
                MessageBoxResult r = MessageBox.Show("You have to log in with Twitter in order to use Ocell.", "", MessageBoxButton.OKCancel);
                if (r == MessageBoxResult.OK)
                    NavigationService.Navigate(new Uri("/Pages/Settings/OAuth.xaml", UriKind.Relative));
            });
        }

        void GetPivotsFromConf()
        {
            foreach(var pivot in Config.Columns)
                if(!pivots.Contains(pivot))
                    pivots.Add(pivot);
        }

        void UpdatePivotTitle()
        {
            Dispatcher.BeginInvoke(() =>
            {
                MainPivot.Title = "OCELL";
                if(DataTransfer.CurrentAccount != null && !string.IsNullOrWhiteSpace(DataTransfer.CurrentAccount.ScreenName))
                    MainPivot.Title += " - " + DataTransfer.CurrentAccount.ScreenName.ToUpperInvariant();
            });
        }

        void pivots_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            BindPivots();
        }        

        private void compose_Click(object sender, System.EventArgs e)
        {
            if (Config.Accounts.Count == 0)
                ShowLoginMsg();
            else
                NavigationService.Navigate(new Uri("/Pages/NewTweet.xaml", UriKind.Relative));
        }

        private void ListBox_Loaded(object sender, RoutedEventArgs e)
        {
            ExtendedListBox list = sender as ExtendedListBox;
            TwitterResource Resource = new TwitterResource();

            if(list == null)
                return;

            if(list.Tag is TwitterResource)
            {
                Resource = (TwitterResource)list.Tag;
                list.Bind(Resource);
            }

            if (!Lists.ContainsKey(Resource.String))
                Lists.Add(Resource.String, list);

            list.Compression += new ExtendedListBox.OnCompression(list_Compression);
            list.Loader.Error += new TweetLoader.OnError(Loader_Error);
            list.Loader.LoadFinished += new TweetLoader.OnLoadFinished(Loader_LoadFinished);
            Dispatcher.BeginInvoke(() => pBar.IsVisible = true);
            list.Loader.LoadCache();
            list.Loader.Load();
        }

        void Loader_LoadFinished()
        {
            Dispatcher.BeginInvoke(() => pBar.IsVisible = false);
        }

        void Loader_Error(TwitterResponse response)
        {
            Dispatcher.BeginInvoke(() => {
                MessageBox.Show("Error loading tweets: " + response.StatusDescription);
                pBar.IsVisible = false;
            });
        }

        void list_Compression(object sender, CompressionEventArgs e)
        {
            bool Old = (e.Type == CompressionType.Bottom);
            ExtendedListBox List = sender as ExtendedListBox;

            if (List == null || List.Loader == null)
                return;
            Dispatcher.BeginInvoke(() => pBar.IsVisible = true);
            List.Loader.Load(Old);
        }

        private void menuItem1_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Pages/Settings/Default.xaml", UriKind.Relative));
        }

        private void add_Click(object sender, EventArgs e)
        {
            if (Config.Accounts.Count == 0)
                ShowLoginMsg();
            else
                NavigationService.Navigate(new Uri("/Pages/Columns/ManageColumns.xaml", UriKind.Relative));
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!selectionChangeFired)
            {
                DataTransfer.Status = e.AddedItems[0] as TwitterStatus;
                DataTransfer.DM = e.AddedItems[0] as TwitterDirectMessage;
                ListBox list = sender as ListBox;
                selectionChangeFired = true;
                list.SelectedIndex = -1;
                if (e.AddedItems[0] is TwitterStatus)
                    NavigationService.Navigate(new Uri("/Pages/Tweet.xaml", UriKind.Relative));
                else
                    NavigationService.Navigate(new Uri("/Pages/DMView.xaml", UriKind.Relative));
            }
            else
                selectionChangeFired = false;
        }

        private void MainPivot_Loaded(object sender, RoutedEventArgs e)
        {
            MainPivot.DataContext = pivots;
            MainPivot.ItemsSource = pivots;
        }
    }
}