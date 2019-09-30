﻿// Copyright 2019 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.NetworkAnalysis;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Foundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UIKit;
using Xamarin.Auth;

namespace ArcGISRuntimeXamarin.Samples.NavigateAR
{
    [Register("NavigateAR")]
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        "Navigate in AR",
        "Augmented reality",
        "Use a route displayed in the real world to navigate.",
        "")]
    [ArcGISRuntime.Samples.Shared.Attributes.OfflineData()]
    public class NavigateAR : UIViewController, IOAuthAuthorizeHandler
    {
        // Hold references to the UI controls.
        private MapView _mapView;
        private UILabel _helpLabel;
        private UIBarButtonItem _navigateButton;

        private GraphicsOverlay _routeOverlay;
        private GraphicsOverlay _stopsOverlay;

        private MapPoint _startPoint;
        private MapPoint _endPoint;

        private RouteTask _routeTask;
        private Route _route;
        private RouteResult _routeResult;
        private RouteParameters _routeParameters;

        // Auth
        private TaskCompletionSource<IDictionary<string, string>> _taskCompletionSource;
        private const string ServerUrl = "https://www.arcgis.com/sharing/rest";
        private const string AppClientId = @"lgAdHkYZYlwwfAhC";
        private const string OAuthRedirectUrl = @"my-ags-app://auth";
        private Xamarin.Auth.OAuth2Authenticator _auth;

        public override void LoadView()
        {
            // Create the views.
            View = new UIView();

            _mapView = new MapView();
            _mapView.TranslatesAutoresizingMaskIntoConstraints = false;

            _helpLabel = new UILabel();
            _helpLabel.TranslatesAutoresizingMaskIntoConstraints = false;
            _helpLabel.TextAlignment = UITextAlignment.Center;
            _helpLabel.TextColor = UIColor.White;
            _helpLabel.BackgroundColor = UIColor.FromWhiteAlpha(0f, 0.6f);
            _helpLabel.Text = "Preparing services...";

            UIToolbar toolbar = new UIToolbar();
            toolbar.TranslatesAutoresizingMaskIntoConstraints = false;

            _navigateButton = new UIBarButtonItem("Navigate", UIBarButtonItemStyle.Plain, _navigateButton_Click);
            _navigateButton.Enabled = false;

            toolbar.Items = new[]
            {
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                _navigateButton,
                new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace)
            };

            // Add the views.
            View.AddSubviews(_mapView, _helpLabel, toolbar);

            // Lay out the views.
            NSLayoutConstraint.ActivateConstraints(new[]{
                _mapView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
                _mapView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
                _mapView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _mapView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _helpLabel.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                _helpLabel.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _helpLabel.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _helpLabel.HeightAnchor.ConstraintEqualTo(40),
                toolbar.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                toolbar.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                toolbar.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor)
            });
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            Initialize();
        }

        private async void Initialize()
        {
            _mapView.Map = new Map(Basemap.CreateImagery());

            try
            {
                _mapView.LocationDisplay.AutoPanMode = LocationDisplayAutoPanMode.Recenter;
                await _mapView.LocationDisplay.DataSource.StartAsync();
                _mapView.LocationDisplay.IsEnabled = true;

                SetOAuthInfo();

                _routeTask = await RouteTask.CreateAsync(new System.Uri("https://route.arcgis.com/arcgis/rest/services/World/Route/NAServer/Route_World"));

                _routeOverlay = new GraphicsOverlay();
                SimpleLineSymbol routeSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Yellow, 1);
                _routeOverlay.Renderer = new SimpleRenderer(routeSymbol);

                _stopsOverlay = new GraphicsOverlay();
                SimpleMarkerSymbol stopSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 5);
                _stopsOverlay.Renderer = new SimpleRenderer(stopSymbol);

                _mapView.GraphicsOverlays.Add(_routeOverlay);
                _mapView.GraphicsOverlays.Add(_stopsOverlay);

                _mapView.GeoViewTapped += _mapView_GeoViewTapped;

                _helpLabel.Text = "Tap to set a start point";
            }
            catch (System.Exception ex)
            {
                ShowMessage("Failed to start sample", "Error");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void _mapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            if (_startPoint == null)
            {
                _startPoint = e.Location;

                Graphic startGraphic = new Graphic(_startPoint, new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Cross, System.Drawing.Color.Green, 25));
                _stopsOverlay.Graphics.Add(startGraphic);

                _helpLabel.Text = "Tap to set an end point";
            }
            else if (_endPoint == null)
            {
                _endPoint = e.Location;

                Graphic endGraphic = new Graphic(_endPoint, new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.X, System.Drawing.Color.Red, 25));
                _stopsOverlay.Graphics.Add(endGraphic);

                SolveRoute();
            }
        }

        private void EnableNavigation()
        {
            _navigateButton.Enabled = true;

            _helpLabel.Text = "You're ready to start navigating!";
        }

        private void _navigateButton_Click(object sender, System.EventArgs e)
        {
            NavigationController.PushViewController(new RouteViewerAR() { _routeResult = _routeResult }, true);
        }

        private async void SolveRoute()
        {
            _helpLabel.Text = "Solving route";

            try
            {
                _routeParameters = await _routeTask.CreateDefaultParametersAsync();

                _routeParameters.ReturnStops = true;
                _routeParameters.ReturnDirections = true;
                _routeParameters.ReturnRoutes = true;

                TravelMode walkingMode = _routeTask.RouteTaskInfo.TravelModes.FirstOrDefault(mode => mode.Name.Contains("Walk")) ?? _routeTask.RouteTaskInfo.TravelModes.First();
                _routeParameters.TravelMode = walkingMode;

                Stop stop1 = new Stop(_startPoint);
                Stop stop2 = new Stop(_endPoint);

                _routeParameters.SetStops(new[] { stop1, stop2 });

                _routeResult = await _routeTask.SolveRouteAsync(_routeParameters);

                _route = _routeResult.Routes.First();

                Graphic routeGraphic = new Graphic(_route.RouteGeometry);
                _routeOverlay.Graphics.Add(routeGraphic);

                _helpLabel.Text = "Route calculated.";

                EnableNavigation();
            }
            catch (Exception ex)
            {
                ShowMessage("Failed to calculate route", "Error");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void ShowMessage(string message, string title)
        {
            // Create Alert.
            UIAlertController okAlertController = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);

            // Add Action.
            okAlertController.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));

            // Present Alert.
            PresentViewController(okAlertController, true, null);
        }

        private void SetOAuthInfo()
        {
            // Register the server information with the AuthenticationManager.
            ServerInfo serverInfo = new ServerInfo
            {
                ServerUri = new Uri(ServerUrl),
                TokenAuthenticationType = TokenAuthenticationType.OAuthImplicit,
                OAuthClientInfo = new OAuthClientInfo
                {
                    ClientId = AppClientId,
                    RedirectUri = new Uri(OAuthRedirectUrl)
                }
            };

            // Register this server with AuthenticationManager.
            AuthenticationManager.Current.RegisterServer(serverInfo);

            // Use a function in this class to challenge for credentials.
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(CreateCredentialAsync);

            // Set the OAuthAuthorizeHandler component (this class).
            AuthenticationManager.Current.OAuthAuthorizeHandler = this;
        }

        #region OAuth helpers

        // ChallengeHandler function that will be called whenever access to a secured resource is attempted.
        private async Task<Credential> CreateCredentialAsync(CredentialRequestInfo info)
        {
            Credential credential = null;

            try
            {
                // IOAuthAuthorizeHandler will challenge the user for OAuth credentials.
                credential = await AuthenticationManager.Current.GenerateCredentialAsync(info.ServiceUri);
            }
            catch (TaskCanceledException)
            {
                return credential;
            }
            catch (Exception)
            {
                // Exception will be reported in calling function.
                throw;
            }

            return credential;
        }

        // IOAuthAuthorizeHandler.AuthorizeAsync implementation.
        public Task<IDictionary<string, string>> AuthorizeAsync(Uri serviceUri, Uri authorizeUri, Uri callbackUri)
        {
            // If the TaskCompletionSource is not null, authorization is in progress.
            if (_taskCompletionSource != null)
            {
                // Allow only one authorization process at a time.
                throw new Exception();
            }

            // Create a task completion source.
            _taskCompletionSource = new TaskCompletionSource<IDictionary<string, string>>();

            // Create a new Xamarin.Auth.OAuth2Authenticator using the information passed in.
            _auth = new OAuth2Authenticator(
                clientId: AppClientId,
                scope: "",
                authorizeUrl: authorizeUri,
                redirectUrl: new Uri(OAuthRedirectUrl))
            {
                // Allow the user to cancel the OAuth attempt.
                AllowCancel = true
            };

            // Define a handler for the OAuth2Authenticator.Completed event.
            _auth.Completed += (object sender, AuthenticatorCompletedEventArgs args) =>
            {
                try
                {
                    // Dismiss the OAuth UI when complete.
                    this.DismissViewController(true, null);

                    // Throw an exception if the user could not be authenticated.
                    if (!args.IsAuthenticated)
                    {
                        throw new Exception("Unable to authenticate user.");
                    }

                    // If authorization was successful, get the user's account.
                    Xamarin.Auth.Account authenticatedAccount = args.Account;

                    // Set the result (Credential) for the TaskCompletionSource.
                    _taskCompletionSource.SetResult(authenticatedAccount.Properties);
                }
                catch (Exception ex)
                {
                    // If authentication failed, set the exception on the TaskCompletionSource.
                    _taskCompletionSource.SetException(ex);
                }
            };

            // If an error was encountered when authenticating, set the exception on the TaskCompletionSource.
            _auth.Error += (object sender, AuthenticatorErrorEventArgs args) =>
            {
                if (args.Exception != null)
                {
                    _taskCompletionSource.TrySetException(args.Exception);
                }
                else
                {
                    _taskCompletionSource.TrySetException(new Exception(args.Message));
                }
            };

            // Present the OAuth UI (on the app's UI thread) so the user can enter user name and password.
            InvokeOnMainThread(() => { this.PresentViewController(_auth.GetUI(), true, null); });

            // Return completion source task so the caller can await completion.
            return _taskCompletionSource.Task;
        }

        #endregion
    }
}