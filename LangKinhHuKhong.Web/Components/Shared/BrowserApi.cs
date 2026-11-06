using System.Text.Json;
using Microsoft.JSInterop;

namespace LangKinhHuKhong.Components.Shared;

// Scoped per Blazor Server circuit. Never depend on a global function that may
// not have loaded at the time the first OnAfterRenderAsync runs.
// Importing the JS module guarantees site.js has executed before invocation.
public sealed class BrowserApi(IJSRuntime js) : IAsyncDisposable
{
    private Task<IJSObjectReference>? moduleTask;
    private const string ModulePath = "/js/lkh-interop.js?v=1.9.5.0";

    private async Task<IJSObjectReference> ModuleAsync()
    {
        var pending = moduleTask ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
        try { return await pending; }
        catch
        {
            // A transient network error must not permanently poison this circuit.
            if (ReferenceEquals(moduleTask, pending)) moduleTask = null;
            throw;
        }
    }

    private static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan LongUploadTimeout = TimeSpan.FromMinutes(20);

    private async ValueTask<JsonElement> CallJson(string method, params object?[] args)
        => await CallJsonWithTimeout(method, DefaultCallTimeout, args);

    private async ValueTask<JsonElement> CallJsonWithTimeout(string method, TimeSpan timeout, params object?[] args)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await (await ModuleAsync()).InvokeAsync<JsonElement>(method, cts.Token, args);
    }

    private async ValueTask CallVoid(string method, params object?[] args)
        => await (await ModuleAsync()).InvokeVoidAsync(method, args);

    public ValueTask<JsonElement> Get(string path) => CallJson("api", path, "GET", null);
    public ValueTask<JsonElement> Post(string path, object? model = null) => CallJsonWithTimeout("api", LongUploadTimeout, path, "POST", model);
    public ValueTask<JsonElement> Put(string path, object model) => CallJsonWithTimeout("api", LongUploadTimeout, path, "PUT", model);
    public ValueTask<JsonElement> Delete(string path) => CallJson("api", path, "DELETE", null);
    public ValueTask<JsonElement> ReplaceAudio(string episodeId) => CallJsonWithTimeout("replaceEpisodeAudio", LongUploadTimeout, episodeId);
    public ValueTask<JsonElement> Upload(string storyId, string title, int number) => CallJsonWithTimeout("uploadEpisode", LongUploadTimeout, storyId, title, number);
    public ValueTask<JsonElement> UploadCover(string storyId) => CallJsonWithTimeout("uploadCover", LongUploadTimeout, storyId);
    public ValueTask<JsonElement> UploadStoryTexts(string storyId, string title, string inputId = "story-text-files") => CallJsonWithTimeout("uploadStoryTexts", LongUploadTimeout, storyId, title, inputId);
    public ValueTask<JsonElement> OpenNotepadFile(string textId) => CallJsonWithTimeout("openNotepadFile", LongUploadTimeout, textId);
    public ValueTask<JsonElement> InitNewNotepadFile(string initialContent = "") => CallJson("initNewNotepadFile", initialContent);
    public ValueTask<JsonElement> SaveNotepadFile(string textId, string storyId, string title, string fileName, string extension)
        => CallJsonWithTimeout("saveNotepadFile", LongUploadTimeout, textId, storyId, title, fileName, extension);
    public ValueTask<JsonElement> CopyNotepadContent() => CallJson("copyNotepadContent");
    public ValueTask Listen(string episodeId) => CallVoid("listen", episodeId);
    public ValueTask StopAudio() => CallVoid("stopAudio");
    public ValueTask BindAutoNext(object? receiver) => CallVoid("bindAutoNext", receiver);
    public ValueTask Reload() => CallVoid("reload");

    public async ValueTask DisposeAsync()
    {
        if (moduleTask is { IsCompletedSuccessfully: true } loaded)
        {
            try { await loaded.Result.DisposeAsync(); }
            catch (JSDisconnectedException) { /* Circuit closed. */ }
            catch (ObjectDisposedException) { /* Circuit disposing. */ }
        }
    }
}
