﻿using Ocell.Library;
using Ocell.Library.Twitter;
using Ocell.Localization;
using Ocell.Pages;
using PropertyChanged;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace Ocell
{
    [ImplementPropertyChanged]
    public class ManageDraftsModel : ExtendedViewModelBase
    {
        public ObservableCollection<TwitterDraft> Collection { get; set; }

        public object ListSelection { get; set; }

        public ManageDraftsModel()
        {
            Collection = new ObservableCollection<TwitterDraft>(Config.Drafts.Value);

            this.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == "ListSelection")
                        OnSelectionChanged();
                };
        }

        public override void OnNavigating(System.ComponentModel.CancelEventArgs e)
        {
            base.OnNavigating(e);

            Config.Drafts.Value = new List<TwitterDraft>(Collection);
        }

        public void GridHold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            Grid grid = sender as Grid;
            if (grid == null)
                return;

            TwitterDraft draft = grid.Tag as TwitterDraft;
            if (draft != null && Config.Drafts.Value.Contains(draft))
            {
                var accepts = Notificator.Prompt(Resources.AskDeleteDraft);
                if (accepts)
                {
                    Collection.Remove(draft);
                    Notificator.ShowMessage(Resources.DraftDeleted);
                }
            }
        }

        public void OnSelectionChanged()
        {
            TwitterDraft draft = ListSelection as TwitterDraft;

            if (draft == null)
                return;

            Messager.SendTo<NewTweetModel, TwitterDraft>(draft);
            ListSelection = null;
            Navigator.GoBack();
        }

    }
}