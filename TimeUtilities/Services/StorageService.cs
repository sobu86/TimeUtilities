﻿using BlazorUtils.Firebase;
using BlazorUtils.JsInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static BlazorUtils.Firebase.FirebaseGoogleAuthResult;
using static TimeUtilities.Services.IStorageService;

namespace TimeUtilities.Services
{
    internal class StorageService : IStorageService
    {
        private const string TrackedTimezonesKey = "trackedTimezones";

        private ILogger Logger { get; set; }
        private IJsInteropService JSR { get; set; }
        private IFirebaseGoogleAuthService AuthService { get; set; }
        private IFirestoreService FirestoreService { get; set; }

        private bool _subscribedForDocUpdates;
        public TrackedTimezoneListUpdateCallbackType TrackedTimezoneListUpdateCallback { get; set; }

        public StorageService(
            ILogger<StorageService> logger, IJsInteropService jsInteropService,
            IFirebaseGoogleAuthService authService, IFirestoreService firestoreService)
        {
            Logger = logger;
            JSR = jsInteropService;
            AuthService = authService;
            FirestoreService = firestoreService;

            AuthService.AuthStateChangedCallback += async (GoogleAuthUser user) =>
            {
                if (user != null)
                {
                    // signed in
                    if (!_subscribedForDocUpdates)
                    {
                        FirestoreOperationResult<UserDocument> res =
                            await FirestoreService.SubscribeForDocumentUpdates<UserDocument>(
                                "users", user.uid,  async document =>
                                {
                                    // Update local storage with firestore content
                                    UserDocument userDoc = document as UserDocument;
                                    if (userDoc.TrackedTimezoneIds == null || userDoc.TrackedTimezoneIds.Count == 0)
                                    {
                                        await DeleteAllTrackedTimezones();
                                    }
                                    else
                                    {
                                        await SaveTrackedTimezones(userDoc.TrackedTimezoneIds);
                                    }

                                    TrackedTimezoneListUpdateCallback.Invoke();
                                });

                        if (res.Success)
                        {
                            Logger.LogInformation("Subscribed for timezone list updates.");
                            _subscribedForDocUpdates = true;
                        }
                        else
                        {
                            Logger.LogError("Failed to subscribe for timezone list updates.");
                        }
                    }
                }
            };
        }

        private class UserDocument : IFirestoreService.IFirestoreDocument
        {
            public IFirestoreService.FirestoreDocRef DocRef { get; set; }

            public ISet<string> TrackedTimezoneIds { get; set; }
        }

        public async Task SaveTrackedTimezones(ISet<string> timezoneIds)
        {
            if (timezoneIds == null || timezoneIds.Count == 0)
            {
                Logger.LogError("No timezones to save.");
                return;
            }

            // Save to firestore
            var user = await AuthService.GetCurrentUser();
            if (user != null)
            {
                // Collection
                string collection = "users";
                string docId = user.uid;
                UserDocument doc = new UserDocument() { TrackedTimezoneIds = timezoneIds };

                FirestoreOperationResult<UserDocument> result =
                    await FirestoreService.UpdateDocument<UserDocument, UserDocument>(collection, docId, doc);
                Logger.LogInformation($"Doc save result: {result.Success}");
            }

            // Let's save to local storage as well
            // We will retrieve this if user is not signed in
            try
            {
                await JSR.StorageUtils.LocalStorageSetItem(
                    TrackedTimezonesKey, JsonSerializer.Serialize(timezoneIds));
            }
            catch(Exception)
            {
                Logger.LogError("Failed to save to local storage.");
            }
        }

        public async Task<ISet<string>> GetTrackedTimezones()
        {
            // Get from firestore
            var user = await AuthService.GetCurrentUser();
            if (user != null)
            {
                // Collection
                string collection = "users";
                string docId = user.uid;

                FirestoreOperationResult<UserDocument> result =
                    await FirestoreService.GetDocument<UserDocument>(collection, docId);
                Logger.LogInformation($"Doc get result: {result.Success}");

                if (result.Success && result.Document != null)
                {
                    return result.Document.TrackedTimezoneIds;
                }
            }
            else
            {
                try
                {
                    string trackedTimezonesJson =
                        await JSR.StorageUtils.LocalStorageGetItem(TrackedTimezonesKey);
                    return JsonSerializer.Deserialize<ISet<string>>(trackedTimezonesJson);
                }
                catch (Exception)
                {
                    Logger.LogError("Failed to get from local storage.");
                }
            }

            return null;
        }

        public async Task DeleteAllTrackedTimezones()
        {
            try
            {
                await JSR.StorageUtils.LocalStorageDeleteItem(TrackedTimezonesKey);
            }
            catch(Exception)
            {
                Logger.LogError("Failed to delete from local storage.");
            }

            // Save to firestore
            var user = await AuthService.GetCurrentUser();
            if (user != null)
            {
                // Collection
                string collection = "users";
                string docId = user.uid;
                UserDocument doc = new UserDocument();

                FirestoreOperationResult<UserDocument> result =
                    await FirestoreService.UpdateDocument<UserDocument, UserDocument>(collection, docId, doc);
                Logger.LogInformation($"Doc save result: {result.Success}");
            }
        }
    }
}
