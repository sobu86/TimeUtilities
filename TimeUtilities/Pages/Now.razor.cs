﻿using BlazorUtils.Firebase;
using BlazorUtils.JsInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TimeUtilities.Services;

namespace TimeUtilities.Pages
{
    public partial class Now
    {
        // Framework Injections
        [Inject]
        private ILogger<Index> Logger { get; set; }

        [Inject]
        private IJsInteropService JSR { get; set; }

        [Inject]
        private IStorageService SSR { get; set; }

        [Inject]
        private IFirebaseGoogleAuthService AuthService { get; set; }

        private readonly Timer _uiRefreshTimer = new Timer(250);

        // Fields
        private DateTime _utcNow;
        private DateTime _localNow;
        private int _localTzOffset = -1;
        private string _localTzName = "";

        // List of timezone card displayed in this page
        private ISet<string> _timezoneIdList = new HashSet<string>();

        private string LocalTzOffsetStr
        {
            get
            {
                if (_localTzOffset == -1)
                {
                    return "NA";
                }
                else
                {
                    int hours = Math.Abs(_localTzOffset) / 60;
                    int minutes = Math.Abs(_localTzOffset) % 60;
                    string sign = _localTzOffset < 0 ? "-" : "+";
                    return hours > 0 ?
                        $"UTC ({sign}) {hours} hours and {minutes} minutes" :
                        $"UTC ({sign}) {minutes} minutes";
                }
            }
        }

        protected override async Task OnInitializedAsync()
        {
            Logger.LogInformation("OnInitializedAsync()");

            _uiRefreshTimer.Elapsed += TimerTick;
            _uiRefreshTimer.Start();

            _localTzOffset = await JSR.TimeUtils.GetLocalTimezoneOffset();
            _localTzName = await JSR.TimeUtils.GetLocalTimezoneName();

            await PopulateTrackedTimezones();

            SSR.TrackedTimezoneListUpdateCallback += async () =>
            {
                await PopulateTrackedTimezones();
                this.StateHasChanged();
            };

            await base.OnInitializedAsync();
        }

        private async Task PopulateTrackedTimezones()
        {
            ISet<string> tt = await SSR.GetTrackedTimezones();
            if (tt != null && !_timezoneIdList.SetEquals(tt))
            {
                _timezoneIdList.Clear();
                if (tt != null)
                {
                    _timezoneIdList.UnionWith(await SSR.GetTrackedTimezones());
                }
            }
            else if (tt == null)
            {
                _timezoneIdList.Clear();
            }
        }

        private void TimerTick(object sender, ElapsedEventArgs args)
        {
            // populate instance variables
            _utcNow = DateTime.UtcNow;
            _localNow = DateTime.UtcNow.Add(TimeSpan.FromMinutes(_localTzOffset));

            // update the UI
            this.StateHasChanged();
        }

        private async void AddTimezonesCallback(ISet<string> timezoneIds)
        {
            Logger.LogInformation($"Adding {timezoneIds.Count} new timezones.");
            _timezoneIdList.UnionWith(timezoneIds);

            // save tracked timezones to local storage
            await SSR.SaveTrackedTimezones(_timezoneIdList);
        }
    }
}
