﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using Microsoft.Practices.Prism.Commands;
using Samba.Domain.Models.Locations;
using Samba.Domain.Models.Tickets;
using Samba.Infrastructure;
using Samba.Localization.Properties;
using Samba.Presentation.Common;
using Samba.Presentation.Common.Services;
using Samba.Presentation.ViewModels;
using Samba.Services;
using Samba.Services.Common;

namespace Samba.Modules.LocationModule
{
    [Export]
    public class LocationSelectorViewModel : ObservableObject
    {
        public DelegateCommand<LocationScreenItemViewModel> LocationSelectionCommand { get; set; }
        public DelegateCommand<LocationScreen> SelectLocationCategoryCommand { get; set; }
        public ICaptionCommand CloseScreenCommand { get; set; }
        public ICaptionCommand EditSelectedLocationScreenPropertiesCommand { get; set; }
        public ICaptionCommand IncPageNumberCommand { get; set; }
        public ICaptionCommand DecPageNumberCommand { get; set; }

        public ObservableCollection<IDiagram> Locations { get; set; }

        private Ticket _selectedTicket;
        public Ticket SelectedTicket
        {
            get { return _selectedTicket; }
            set
            {
                _selectedTicket = value;
                RefreshLocations();
                RaisePropertyChanged(() => SelectedTicket);
            }
        }

        public LocationScreen SelectedLocationScreen { get { return _applicationState.SelectedLocationScreen; } }
        public IEnumerable<LocationScreen> LocationScreens { get { return _applicationState.CurrentDepartment != null ? _applicationState.CurrentDepartment.LocationScreens : null; } }

        public bool CanDesignLocations { get { return _applicationState.CurrentLoggedInUser.UserRole.IsAdmin; } }
        public int CurrentPageNo { get; set; }

        public bool IsPageNavigatorVisible { get { return SelectedLocationScreen != null && SelectedLocationScreen.PageCount > 1; } }
        public bool IsFeedbackVisible { get { return !string.IsNullOrEmpty(Feedback); } }
        private string _feedback;
        public string Feedback
        {
            get { return _feedback; }
            set
            {
                _feedback = value;
                RaisePropertyChanged(() => Feedback);
                RaisePropertyChanged(() => IsFeedbackVisible);
            }
        }

        private string _feedbackColor;
        public string FeedbackColor
        {
            get { return _feedbackColor; }
            set { _feedbackColor = value; RaisePropertyChanged(() => FeedbackColor); }
        }

        private string _feedbackForeground;
        public string FeedbackForeground
        {
            get { return _feedbackForeground; }
            set
            {
                _feedbackForeground = value;
                RaisePropertyChanged(() => FeedbackForeground);
            }
        }

        public VerticalAlignment LocationsVerticalAlignment { get { return SelectedLocationScreen != null && SelectedLocationScreen.ButtonHeight > 0 ? VerticalAlignment.Top : VerticalAlignment.Stretch; } }

        private readonly IApplicationState _applicationState;
        private readonly ILocationService _locationService;
        private readonly IUserService _userService;
        private readonly IApplicationStateSetter _applicationStateSetter;

        [ImportingConstructor]
        public LocationSelectorViewModel(IApplicationState applicationState, IApplicationStateSetter applicationStateSetter,
            ILocationService locationService, IUserService userService)
        {
            _applicationState = applicationState;
            _applicationStateSetter = applicationStateSetter;
            _locationService = locationService;
            _userService = userService;
            SelectLocationCategoryCommand = new DelegateCommand<LocationScreen>(OnSelectLocationCategoryExecuted);
            LocationSelectionCommand = new DelegateCommand<LocationScreenItemViewModel>(OnSelectLocationExecuted);
            CloseScreenCommand = new CaptionCommand<string>(Resources.Close, OnCloseScreenExecuted);
            EditSelectedLocationScreenPropertiesCommand = new CaptionCommand<string>(Resources.Properties, OnEditSelectedLocationScreenProperties, CanEditSelectedLocationScreenProperties);
            IncPageNumberCommand = new CaptionCommand<string>(Resources.NextPage + " >>", OnIncPageNumber, CanIncPageNumber);
            DecPageNumberCommand = new CaptionCommand<string>("<< " + Resources.PreviousPage, OnDecPageNumber, CanDecPageNumber);

            EventServiceFactory.EventService.GetEvent<GenericEvent<Message>>().Subscribe(
                x =>
                {
                    if (_applicationState.ActiveAppScreen == AppScreens.LocationList
                        && x.Topic == EventTopicNames.MessageReceivedEvent
                        && x.Value.Command == Messages.TicketRefreshMessage)
                    {
                        RefreshLocations();
                    }
                });
        }

        public void RefreshLocations()
        {
            if (SelectedLocationScreen == null && LocationScreens.Count() > 0)
                _applicationStateSetter.SetSelectedLocationScreen(LocationScreens.First());
            if (SelectedLocationScreen != null)
                UpdateLocations(SelectedLocationScreen);
        }

        private bool CanDecPageNumber(string arg)
        {
            return SelectedLocationScreen != null && CurrentPageNo > 0;
        }

        private void OnDecPageNumber(string obj)
        {
            CurrentPageNo--;
            RefreshLocations();
        }

        private bool CanIncPageNumber(string arg)
        {
            return SelectedLocationScreen != null && CurrentPageNo < SelectedLocationScreen.PageCount - 1;
        }

        private void OnIncPageNumber(string obj)
        {
            CurrentPageNo++;
            RefreshLocations();
        }

        private bool CanEditSelectedLocationScreenProperties(string arg)
        {
            return SelectedLocationScreen != null;
        }

        private void OnEditSelectedLocationScreenProperties(string obj)
        {
            if (SelectedLocationScreen != null)
                InteractionService.UserIntraction.EditProperties(SelectedLocationScreen);
        }

        private void OnCloseScreenExecuted(string obj)
        {
            _applicationState.CurrentDepartment.PublishEvent(EventTopicNames.ActivateOpenTickets);
        }

        private void OnSelectLocationCategoryExecuted(LocationScreen obj)
        {
            UpdateLocations(obj);
        }

        private static void OnSelectLocationExecuted(LocationScreenItemViewModel obj)
        {
            var location = new LocationData
                               {
                                   LocationId = obj.Model.Id,
                                   LocationName = obj.Model.Name,
                                   TicketId = obj.Model.TicketId,
                                   Caption = obj.Caption
                               };
            location.PublishEvent(EventTopicNames.LocationSelectedForTicket);
        }

        private void UpdateLocations(LocationScreen locationScreen)
        {
            Feedback = "";
            var locationData = _locationService.GetCurrentLocations(locationScreen, CurrentPageNo).OrderBy(x => x.Order);

            if (Locations != null && (Locations.Count() == 0 || Locations.Count != locationData.Count() || Locations.First().Caption != locationData.First().Name)) Locations = null;

            if (Locations == null)
            {
                Locations = new ObservableCollection<IDiagram>();
                Locations.AddRange(locationData.Select(x =>
                    new LocationScreenItemViewModel(x,
                        SelectedLocationScreen,
                        LocationSelectionCommand,
                        SelectedTicket != null,
                        _userService.IsUserPermittedFor(PermissionNames.MergeTickets))));
            }
            else
            {
                for (var i = 0; i < locationData.Count(); i++)
                {
                    ((LocationScreenItemViewModel)Locations[i]).Model = locationData.ElementAt(i);
                }
            }

            if (SelectedTicket != null && !string.IsNullOrEmpty(SelectedTicket.LocationName))
            {
                FeedbackColor = "Red";
                FeedbackForeground = "White";
                Feedback = string.Format(Resources.SelectLocationThatYouWantToMoveTicket_f, SelectedTicket.LocationName);
            }
            else if (SelectedTicket != null)
            {
                FeedbackColor = "Red";
                FeedbackForeground = "White";
                Feedback = Resources.SelectLocationForTicket;
            }
            else
            {
                FeedbackColor = "LightYellow";
                FeedbackForeground = "Black";
                Feedback = Resources.SelectLocationForOperation;
            }

            RaisePropertyChanged(() => Locations);
            RaisePropertyChanged(() => LocationScreens);
            RaisePropertyChanged(() => SelectedLocationScreen);
            RaisePropertyChanged(() => IsPageNavigatorVisible);
            RaisePropertyChanged(() => LocationsVerticalAlignment);
        }

        public void LoadTrackableLocations()
        {
            Locations = new ObservableCollection<IDiagram>(
                _locationService.LoadLocations(SelectedLocationScreen.Name)
                .Select<Location, IDiagram>(x => new LocationScreenItemViewModel(x, SelectedLocationScreen)));
            RaisePropertyChanged(() => Locations);
        }

        public void SaveTrackableLocations()
        {
            _locationService.SaveLocations();
            UpdateLocations(SelectedLocationScreen);
        }
    }
}
