﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Email;
using Windows.System;
using Biziday.Core.Models;
using Biziday.Core.Modules.App;
using Biziday.Core.Modules.App.Analytics;
using Biziday.Core.Modules.App.Dialogs;
using Biziday.Core.Modules.App.Navigation;
using Biziday.Core.Modules.News.Services;
using Biziday.Core.Repositories;
using Biziday.Core.Validation.Reports.Operation;
using Caliburn.Micro;

namespace Biziday.UWP.ViewModels
{
    public class HomeViewModel : ViewModelBase, IHandle<LocationChangedEvent>
    {
        private readonly INewsRetriever _newsRetriever;
        private readonly IAppStateManager _appStateManager;
        private readonly IPageNavigationService _pageNavigationService;
        private readonly IUserNotificationService _userNotificationService;
        private readonly IStatisticsService _statisticsService;
        private readonly ISettingsRepository _settingsRepository;
        private IncrementalLoadingCollection<NewsItem, NewsRetriever> _newsItems;
        private bool _pageIsLoading;
        private bool _locationIsSelected;
        private string _errorMessage;
        private bool _isErrorVisible;
        private string _searchText;
        private bool _searchIsEnabled;
        private List<NewsItem> _allNews;
        private NewsItem _selectedNews;

        public HomeViewModel(INewsRetriever newsRetriever, IAppStateManager appStateManager,
            IPageNavigationService pageNavigationService, IUserNotificationService userNotificationService, IStatisticsService statisticsService, IEventAggregator eventAggregator, ISettingsRepository settingsRepository)
        {
            _newsRetriever = newsRetriever;
            _appStateManager = appStateManager;
            _pageNavigationService = pageNavigationService;
            _userNotificationService = userNotificationService;
            _statisticsService = statisticsService;
            _settingsRepository = settingsRepository;
            eventAggregator.Subscribe(this);
        }

        protected override async void OnInitialize()
        {
            base.OnInitialize();
            try
            {
                StartWebRequest();
                _newsRetriever.ErrorOccurred += OnFailedToRetrieveNews;
                await _appStateManager.EnableBackgroundTask();
                if (_appStateManager.FirstTimeUse())
                {
                    LocationIsSelected = false;
                    await _userNotificationService.ShowMessageDialogAsync("Disclaimer: Aceasta nu este aplicația oficială Biziday pentru platforma Windows.");
                    await _newsRetriever.RetrieveNews(1);
                }
                else
                    LocationIsSelected = _appStateManager.LocationIsSelected();
                ResetNews();
            }
            finally
            {
                EndWebRequest();
            }
        }

        private void OnFailedToRetrieveNews(object sender, BasicReport e)
        {
            ErrorMessage = e.ErrorMessage;
            IsErrorVisible = true;
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            LocationIsSelected = _appStateManager.LocationIsSelected();
            var stringId = _settingsRepository.GetLocalData<string>(SettingsKey.ToastNewsId);
            LoadNewsFromToast(stringId);
            _statisticsService.RegisterPage("news");
        }

        public void LoadNewsFromToast(string stringId)
        {
            int id;
            if (string.IsNullOrWhiteSpace(stringId) == false && int.TryParse(stringId, out id))
            {
                _settingsRepository.SetLocalData(SettingsKey.ToastNewsId, string.Empty);
                SelectedNews = NewsItems.FirstOrDefault(n => n.Id == id);
            }
        }

        public IncrementalLoadingCollection<NewsItem, NewsRetriever> NewsItems
        {
            get { return _newsItems; }
            set
            {
                if (Equals(value, _newsItems)) return;
                _newsItems = value;
                NotifyOfPropertyChange(() => NewsItems);
            }
        }

        public async Task SetRating()
        {
            await Launcher.LaunchUriAsync(new Uri(string.Format("ms-windows-store:REVIEW?PFN={0}", Windows.ApplicationModel.Package.Current.Id.FamilyName)));
            _statisticsService.RegisterEvent(EventCategory.AppEvent, "rating", DateTime.Now.ToString("F"));
        }

        public async Task SendFeedback()
        {
            var emailMessage = new EmailMessage { Subject = "Feedback Biziday" };
            emailMessage.To.Add(new EmailRecipient("bogdan@thewindev.net"));
            await EmailManager.ShowComposeNewEmailAsync(emailMessage);
            _statisticsService.RegisterEvent(EventCategory.AppEvent, "feedback", DateTime.Now.ToString("F"));
        }

        public void RefreshNews()
        {
            _newsRetriever.Refresh();
            ResetNews();
            IsErrorVisible = false;
        }

        private void ResetNews()
        {
            NewsItems = new IncrementalLoadingCollection<NewsItem, NewsRetriever>(_newsRetriever as NewsRetriever);
        }

        public bool PageIsLoading
        {
            get { return _pageIsLoading; }
            set
            {
                if (value == _pageIsLoading) return;
                _pageIsLoading = value;
                NotifyOfPropertyChange(() => PageIsLoading);
            }
        }

        public bool LocationIsSelected
        {
            get { return _locationIsSelected; }
            set
            {
                if (value == _locationIsSelected) return;
                _locationIsSelected = value;
                NotifyOfPropertyChange(() => LocationIsSelected);
            }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set
            {
                if (value == _errorMessage) return;
                _errorMessage = value;
                NotifyOfPropertyChange(() => ErrorMessage);
            }
        }

        public bool IsErrorVisible
        {
            get { return _isErrorVisible; }
            set
            {
                if (value == _isErrorVisible) return;
                _isErrorVisible = value;
                NotifyOfPropertyChange(() => IsErrorVisible);
            }
        }

        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (value == _searchText) return;
                _searchText = value;
                if (_searchText.Length >= 2)
                {
                    FilterNews();
                }
                else if (string.IsNullOrWhiteSpace(_searchText) && SearchIsEnabled)
                {
                    var newsRetriever = _newsRetriever as IncrementalItemSourceBase<NewsItem>;
                    newsRetriever?.RaiseHasMoreItemsChanged(true);
                }
                NotifyOfPropertyChange(() => SearchText);
            }
        }

        private void FilterNews()
        {
            var newsRetriever = _newsRetriever as IncrementalItemSourceBase<NewsItem>;
            newsRetriever?.RaiseHasMoreItemsChanged(false);
            var matchingNews = _allNews.Where(n => n.Body.ToLower().Contains(SearchText.ToLower())).ToList();
            NewsItems.Clear();
            if (matchingNews.Count == 0)
                return;
            foreach (var newsItem in matchingNews)
            {
                NewsItems.Add(newsItem);
            }
        }

        public bool SearchIsEnabled
        {
            get { return _searchIsEnabled; }
            set
            {
                if (value == _searchIsEnabled) return;
                _searchIsEnabled = value;
                NotifyOfPropertyChange(() => SearchIsEnabled);
            }
        }

        public NewsItem SelectedNews
        {
            get { return _selectedNews; }
            set
            {
                if (Equals(value, _selectedNews)) return;
                _selectedNews = value;
                NotifyOfPropertyChange(() => SelectedNews);
            }
        }

        public void SelectLocation()
        {
            _pageNavigationService.NavigateTo<LocationViewModel>();
        }

        public void LoadingPage()
        {
            PageIsLoading = true;
            _statisticsService.RegisterEvent(EventCategory.AppEvent, "news", "page_loading");
        }

        public void PageLoaded()
        {
            PageIsLoading = false;
            _statisticsService.RegisterEvent(EventCategory.AppEvent, "news", "page_loaded");
        }

        public void SearchNews()
        {
            SearchIsEnabled = true;
            _allNews = NewsItems.ToList();
        }

        public void HideSearch()
        {
            SearchText = string.Empty;
            SearchIsEnabled = false;
            NewsItems.Clear();
            foreach (var newsItem in _allNews)
            {
                NewsItems.Add(newsItem);
            }
        }

        public void Handle(LocationChangedEvent message)
        {
            RefreshNews();
        }
    }
}