﻿using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Controls;
using TweetSharp;
using Ocell.Library;
using Ocell.Library.Twitter;

namespace Ocell.Pages.Search
{
    public partial class Search : PhoneApplicationPage
    {
        private ResourceViewModel viewModel;

        public Search()
        {
            InitializeComponent(); Loaded += (sender, e) => { if (ApplicationBar != null) ApplicationBar.MatchOverriddenTheme(); };
            viewModel = new ResourceViewModel();
            DataContext = viewModel;

            
        }

        private void TweetList_Loaded(object sender, RoutedEventArgs e)
        {
            viewModel.Listbox = TweetList;

            TweetList.AutoManageNavigation = true;
            TweetList.ActivatePullToRefresh = true;
        }
    }
}