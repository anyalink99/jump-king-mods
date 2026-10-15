using System;
using System.IO;
using Steamworks;

namespace WorldsmithExtension
{
    internal sealed class PublishRequest
    {
        internal string Root, Content, Title, Description, Changelog, Preview;
        internal string[] Tags;
        internal ulong Id;
        internal int Visibility;
        internal void ValidateFields()
        {
            if (String.IsNullOrWhiteSpace(Title) || Title.Length < 3 || Title.Length > 128)
                throw new InvalidDataException("Title must contain 3..128 characters.");
            if (Visibility < 0 || Visibility > 3)
                throw new InvalidDataException("Invalid visibility.");
            if (Tags == null || Tags.Length == 0)
                throw new InvalidDataException("Workshop category is missing.");
            if (!String.IsNullOrWhiteSpace(Preview) && !File.Exists(Preview))
                throw new FileNotFoundException("Choose a local preview image.", Preview);
        }

        internal void Validate()
        {
            ValidateFields();
            BuildReceipt.Validate(Root, Content);
        }
    }

    internal interface IWorkshop
    {
        void Create(Action<ulong, string, bool> result);
        void Submit(PublishRequest request, Action<string, bool> result);
        string Progress();
    }

    // workflow owns the job until the submit callback arrives
    // a timeout doesn't cancel it, keep the UI locked so we don't upload twice
    internal sealed class Publisher
    {
        readonly IWorkshop api;
        readonly Action<Action, Action<Exception>> verify;
        bool verifying;
        internal bool Busy { get; private set; }

        internal Publisher(IWorkshop api, Action<Action, Action<Exception>> background = null)
        {
            this.api = api;
            verify = background ?? ((work, done) =>
            {
                Exception error = null;
                try { work(); } catch (Exception e) { error = e; }
                done(error);
            });
        }

        internal void Start(PublishRequest request, Action<ulong> persistId, Action<string, bool> finished)
        {
            if (Busy) throw new InvalidOperationException("A Workshop operation is still running.");
            request.ValidateFields();
            Busy = true;
            bool agreement = false;
            Action<string, bool> finish = (message, ok) =>
            {
                Busy = false; verifying = false;
                finished(message, ok);
            };
            Action submit = () =>
            {
                try
                {
                    api.Submit(request, (error, legal) => finish(error ?? (legal || agreement
                        ? "Upload accepted. Accept the Steam Workshop agreement on the item's page."
                        : "Upload completed."), error == null));
                }
                catch (Exception error) { finish(error.Message, false); }
            };
            Action<Action> check = next =>
            {
                verifying = true;
                verify(request.Validate, error =>
                {
                    verifying = false;
                    if (error != null) { finish(error.GetBaseException().Message, false); return; }
                    next();
                });
            };
            try
            {
                check(() =>
                {
                    if (request.Id != 0) { submit(); return; }
                    try
                    {
                        api.Create((id, error, legal) =>
                        {
                            if (error != null || id == 0)
                            {
                                finish(error ?? "Steam returned an empty item ID.", false);
                                return;
                            }
                            request.Id = id;
                            agreement = legal;
                            try { persistId(id); }
                            catch (Exception failure)
                            {
                                finish("Created Workshop item " + id + ", but could not save its ID: " + failure.Message + ". Retry this item after fixing the save error.", false);
                                return;
                            }
                            // creating the item can take minutes, check the files again before submit
                            try { check(submit); }
                            catch (Exception failure) { finish(failure.Message, false); }
                        });
                    }
                    catch (Exception error) { finish(error.Message, false); }
                });
            }
            catch { Busy = false; verifying = false; throw; }
        }

        internal string Progress()
        {
            return verifying ? "Checking project and build files..." : api.Progress();
        }
    }

    internal sealed class SteamWorkshop : IWorkshop
    {
        CallResult<CreateItemResult_t> create;
        CallResult<SubmitItemUpdateResult_t> submit;
        UGCUpdateHandle_t handle;
        DateTime started;
        bool uploading;
        static string Result(EResult result, bool io)
        {
            return io ? "Steam transport failure (" + result + ")." : result == EResult.k_EResultOK ? null : "Steam rejected the request: " + result + ".";
        }

        public void Create(Action<ulong, string, bool> result)
        {
            started = DateTime.UtcNow;
            if (!SteamUser.BLoggedOn())
                throw new IOException("Steam is offline. Sign in before publishing.");
            create = CallResult<CreateItemResult_t>.Create((r, io) => result(r.m_nPublishedFileId.m_PublishedFileId, Result(r.m_eResult, io), r.m_bUserNeedsToAcceptWorkshopLegalAgreement));
            var call = SteamUGC.CreateItem(new AppId_t(1061090), EWorkshopFileType.k_EWorkshopFileTypeCommunity);
            if (call.m_SteamAPICall == 0)
                throw new IOException("Steam could not start item creation.");
            create.Set(call);
        }

        public void Submit(PublishRequest r, Action<string, bool> result)
        {
            started = DateTime.UtcNow;
            if (!SteamUser.BLoggedOn())
                throw new IOException("Steam is offline. Sign in before publishing.");
            handle = SteamUGC.StartItemUpdate(new AppId_t(1061090), new PublishedFileId_t(r.Id));
            if (handle == UGCUpdateHandle_t.Invalid)
                throw new IOException("Steam could not start an update for item " + r.Id);
            Require(SteamUGC.SetItemContent(handle, r.Content), "content");
            Require(SteamUGC.SetItemTitle(handle, r.Title), "title");
            Require(SteamUGC.SetItemDescription(handle, r.Description ?? ""), "description");
            Require(SteamUGC.SetItemVisibility(handle, (ERemoteStoragePublishedFileVisibility)r.Visibility), "visibility");
            Require(SteamUGC.SetItemTags(handle, r.Tags), "tags");
            if (!String.IsNullOrWhiteSpace(r.Preview))
                Require(SteamUGC.SetItemPreview(handle, Path.GetFullPath(r.Preview)), "preview");
            submit = CallResult<SubmitItemUpdateResult_t>.Create((value, io) =>
            {
                uploading = false;
                result(Result(value.m_eResult, io), value.m_bUserNeedsToAcceptWorkshopLegalAgreement);
            });
            var call = SteamUGC.SubmitItemUpdate(handle, r.Changelog ?? "");
            if (call.m_SteamAPICall == 0)
                throw new IOException("Steam could not submit the update.");
            uploading = true;
            submit.Set(call);
        }

        static void Require(bool value, string field)
        {
            if (!value)
                throw new IOException("Steam rejected the " + field + " field. Nothing was submitted.");
        }

        public string Progress()
        {
            string wait = DateTime.UtcNow - started > TimeSpan.FromMinutes(2) ? " Taking longer than expected; still awaiting Steam. Do not start another upload." : "";
            if (!uploading)
                return "Waiting for Steam item creation." + wait;
            ulong done, total;
            var state = SteamUGC.GetItemUpdateProgress(handle, out done, out total);
            return state + (total == 0 ? "" : " — " + done + " / " + total + " bytes") + wait;
        }
    }
}
