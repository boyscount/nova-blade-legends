NOVA BLADE – UI ASSET PACK

Files are exported as individual PNG assets at the requested target dimensions.
Transparent UI elements use RGBA alpha. Map thumbnails are opaque RGB images.

Unity import recommendations:
- Window frame: Sprite (2D and UI), enable 9-Slice, then set borders manually in Sprite Editor.
- Buttons/cards/icons/crest/ribbon: Sprite (2D and UI), Mesh Type Full Rect, Alpha Is Transparency enabled.
- Map thumbnails: Sprite (2D and UI), use Preserve Aspect in UI Image.
- Use Canvas Scaler = Scale With Screen Size.
- Suggested reference resolution: 1920x1080 for PC; use safe-area-aware layout for mobile.

Important artwork notes:
- The generated main frame still contains a small blue jewel at the top center; this differs from the requested jewel-free top edge.
- The side ornaments are not guaranteed to be perfectly seamless/repeatable for 9-slice. Inspect the border settings and use a sliced test in Unity before production.
- The output dimensions are exact, but some source art is fitted within the canvas to preserve proportions, so transparent padding may remain.
- The thumbs-up ribbon is exported as a compact badge asset.

Source: generated composite UI artwork, cropped and exported into separate assets.
