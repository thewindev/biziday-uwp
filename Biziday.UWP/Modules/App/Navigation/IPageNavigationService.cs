﻿using System.Collections.Generic;

namespace Biziday.UWP.Modules.App.Navigation
{
    public interface IPageNavigationService
    {
        void NavigateTo<T>();

        T GetParameter<T>(NavigationKey key);

        void SetNavigationData(NavigationKey key, object data);

        Dictionary<NavigationKey, object> NavigationData { get; set; }

        bool CanGoBack { get; }

        void GoBack();

        void ClearBackStack();

        void RemoveParameter(NavigationKey navigationKey);
    }
}