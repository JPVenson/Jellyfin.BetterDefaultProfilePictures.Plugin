# Better Default Profile Pictures

<img src="logo.svg" alt="Plugin logo" width="80" align="right"/>

A Jellyfin plugin that generates a colourful initials-based avatar for every user that doesn't have a profile picture set.

## How it works

When a user has no profile image, the plugin generates one on the fly. The background colour is seeded from the user's ID, so the same user always ends up with the same colour. The initials are taken from the display name.

Images can be generated locally (no external connections needed) or fetched from a couple of free third-party services.

## Providers

| Provider | Description |
|----------|-------------|
| **Local** *(default)* | Generated on-server with SkiaSharp. Works offline. |
| **UI Avatars** | Fetched from [ui-avatars.com](https://ui-avatars.com/). Free, no sign-up. |
| **DiceBear** | Fetched from [api.dicebear.com](https://www.dicebear.com/). Many avatar styles to pick from. |

## Local styles

| Style | Description |
|-------|-------------|
| **Circle** *(default)* | Solid circle with initials centred inside. |
| **Square** | Same as above, square instead of circle. |
| **Pixelated** | Identicon-style pixel pattern with initials on top. |

## Name formats

| Format | "John Doe" | "alice" |
|--------|-----------|---------|
| Two Initials | JD | AL |
| First Initial | J | A |
| Full First Name | John | alice |

You can also provide a custom template — use `{0}` as a placeholder for the full display name.

## Installation

Install through the Jellyfin plugin catalogue, or drop the DLL into your plugins directory and restart.

repository url: https://raw.githubusercontent.com/JPVenson/Jellyfin.BetterDefaultProfilePictures.Plugin/refs/heads/master/manifest.json

Once installed, open **Dashboard → Plugins → Better Default Profile Pictures** to pick a provider and tweak the settings.

## Configuration

- **Generate on new user** — create an avatar automatically when a new account is created. On by default.
- **Regenerate all** — there's a scheduled task under Dashboard → Scheduled Tasks that regenerates images for every user in one go, handy after changing the style or provider.

## Building from source

```
dotnet build Jellyfin.Plugin.Template.sln
```

To produce the release artifact:

```
dotnet publish Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj --configuration Release --output artifacts
```


## AI disclaimer

Ai has been utilised to translate the original Pullrequest made to the jellyfin server repository into a plugin form.
