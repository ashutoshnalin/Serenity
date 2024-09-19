using Serenity.Web;

namespace Serenity.Services;

/// <summary>
/// Obsolete subclass of the <see cref="MultipleFileUploadBehavior"/>
/// </summary>
/// <remarks>
/// Creates a new instance of the class
/// </remarks>
/// <param name="storage">Upload storage</param>
/// <param name="uploadProcessor">Upload processor</param>
[Obsolete("Use Serenity.Services.MultipleFileUploadBehavior")]
public abstract class MultipleImageUploadBehavior(IUploadStorage storage, IUploadProcessor uploadProcessor) 
    : MultipleFileUploadBehavior(storage, uploadProcessor)
{
}