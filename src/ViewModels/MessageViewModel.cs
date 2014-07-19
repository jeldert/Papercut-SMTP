﻿// Papercut
// 
// Copyright © 2008 - 2012 Ken Robertson
// Copyright © 2013 - 2014 Jaben Cargman
//  
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//  
// http://www.apache.org/licenses/LICENSE-2.0
//  
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Papercut.ViewModels
{
    using System;
    using System.Reactive.Linq;
    using System.Runtime.InteropServices;
    using System.Windows;

    using Caliburn.Micro;

    using MimeKit;

    using Papercut.Core.Annotations;
    using Papercut.Core.Helper;
    using Papercut.Helpers;
    using Papercut.Views;

    using Serilog;

    public class MessageViewModel : Screen
    {
        readonly ILogger _logger;

        string _htmlFile;

        public MessageViewModel(ILogger logger)
        {
            _logger = logger;
        }

        public string HtmlFile
        {
            get { return _htmlFile; }
            set
            {
                _htmlFile = value;
                NotifyOfPropertyChange(() => HtmlFile);
                NotifyOfPropertyChange(() => HasHtmlFile);
            }
        }

        public bool HasHtmlFile
        {
            get { return !string.IsNullOrWhiteSpace(HtmlFile); }
        }

        [DllImport("urlmon.dll")]
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.Error)]
        static extern int CoInternetSetFeatureEnabled(
            int featureEntry,
            [MarshalAs(UnmanagedType.U4)] int dwFlags,
            bool fEnable);

        public void ShowMessage([NotNull] MimeMessage mailMessageEx)
        {
            if (mailMessageEx == null) throw new ArgumentNullException("mailMessageEx");

            Observable.Start(
                () =>
                {
                    try
                    {
                        return mailMessageEx.CreateHtmlPreviewFile();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            ex,
                            "Exception Saving Browser Temp File for {MailMessage}",
                            mailMessageEx.ToString());
                    }

                    return null;
                }).Subscribe(
                    h => { HtmlFile = h; });
        }

        protected override void OnViewLoaded(object view)
        {
            const int Feature = 21; //FEATURE_DISABLE_NAVIGATION_SOUNDS
            const int SetFeatureOnProcess = 0x00000002;

            base.OnViewLoaded(view);

            var typedView = view as MessageView;

            if (typedView == null)
            {
                _logger.Error("Unable to locate the MessageView to hook the WebBrowser Control");
                return;
            }

            try
            {
                // disable the stupid click sound on navigate
                CoInternetSetFeatureEnabled(Feature, SetFeatureOnProcess, true);
            }
            catch (Exception ex)
            {
                // just have to live with the sound
                _logger.Warning(ex, "Failed to disable the Navigation Sounds on the WebBrowser control");
            }

            this.GetPropertyValues(p => p.HtmlFile)
                .Subscribe(
                    file =>
                    {
                        typedView.htmlView.Source =
                            new Uri(string.IsNullOrWhiteSpace(file) ? "about:blank" : file);
                    });

            Observable
                .FromEvent
                <DependencyPropertyChangedEventHandler, DependencyPropertyChangedEventArgs>(
                    a => ((s, e) => a(e)),
                    h => typedView.IsEnabledChanged += h,
                    h => typedView.IsEnabledChanged -= h)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOnDispatcher()
                .Subscribe(
                    o =>
                    {
                        typedView.htmlView.Visibility = o.NewValue.ToType<bool>()
                                                            ? Visibility.Visible
                                                            : Visibility.Collapsed;
                    });
        }
    }
}