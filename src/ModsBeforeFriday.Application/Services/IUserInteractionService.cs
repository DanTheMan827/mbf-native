namespace ModsBeforeFriday.Application.Services;

public interface IUserInteractionService
{
    Task<bool> ConfirmAsync(string title, string message, string primaryText = "Continue");

    Task ShowMessageAsync(string title, string message);

    Task<IReadOnlyList<string>> PickFilesAsync(params string[] fileTypeFilters);

    Task<string?> PickFileAsync(params string[] fileTypeFilters);

    Task SaveTextAsync(string suggestedFileName, string text, string fileTypeDescription = "Text document", string extension = ".txt");

    Task OpenUriAsync(Uri uri);
}
