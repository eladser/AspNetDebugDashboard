# AspNetFlags

[![NuGet](https://img.shields.io/nuget/v/AspNetFlags.svg)](https://www.nuget.org/packages/AspNetFlags/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/eladser/AspNetDebugDashboard/blob/main/LICENSE)

Feature flags with a toggle UI at `/_flags`, stored locally. No SaaS account, no SDK keys. Flags appear the first time your code checks them, so you flip them without declaring anything up front. Part of the [AspNetDebugDashboard](https://github.com/eladser/AspNetDebugDashboard) suite.

![Flags](https://raw.githubusercontent.com/eladser/AspNetDebugDashboard/main/docs/images/flags-demo.gif)

## Install

```bash
dotnet add package AspNetFlags
```

## Setup

```csharp
using AspNetFlags;

builder.Services.AddFlags();   // 1. register
var app = builder.Build();
app.UseFlags();                // 2. serve /_flags (no-op outside Development)
```

## Checking flags

Inject `IFeatureFlags` and gate whatever you want:

```csharp
public class CheckoutController(IFeatureFlags flags)
{
    public IActionResult Index() =>
        flags.IsEnabled("new-checkout") ? View("NewCheckout") : View("Checkout");
}
```

The first check for an unknown flag registers it (off) so it shows up at `/_flags`. Flip it there and the next check returns the new value.

## What you get

`/_flags` lists every flag your app has touched, each with an on/off switch and when it last changed. Toggling persists immediately; changes take effect on the next `IsEnabled` call. You can also add a flag by name or delete one from the page, and filter the list once it grows. The page polls so flags discovered by other requests show up live.

## Configuration

```csharp
builder.Services.AddFlags(o =>
{
    o.BasePath = "/_flags";       // dashboard route
    o.DatabasePath = "flags.db";  // local LiteDB store
    o.AutoDiscover = true;        // off = a flag must exist before it can be toggled
});
```

## License

MIT.
