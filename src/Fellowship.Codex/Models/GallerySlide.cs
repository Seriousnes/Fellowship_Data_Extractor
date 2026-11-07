using Microsoft.AspNetCore.Components;

namespace Fellowship.Codex.Models;

/// <summary>One image of a <c>Gallery</c>.</summary>
/// <param name="Name">The caption the dialog heads the image with.</param>
/// <param name="Source">The image's address.</param>
/// <param name="Aspect">Width over height, where the image is drawn to a shape of its own rather than its native one.</param>
/// <param name="Overlay">Markup drawn over the image in the dialog, placed in fractions of its width and height.</param>
public sealed record GallerySlide(string Name, string Source, double? Aspect = null, RenderFragment? Overlay = null);
