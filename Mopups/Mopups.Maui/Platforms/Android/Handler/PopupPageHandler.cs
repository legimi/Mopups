﻿using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Mopups.Platforms.Android.Renderers;

namespace Mopups.Pages;

public class PopupPageHandler : PageHandler
{
    public bool _disposed;

    public PopupPageHandler()
    {
        var mauiContext = new MauiContext(MauiApplication.Current.Services, Platform.CurrentActivity);
        SetMauiContext(mauiContext);
    }

    public PopupPageHandler(IMauiContext context)
    {
        SetMauiContext(context);
    }

    protected override void ConnectHandler(ContentViewGroup platformView)
    {
        if (platformView is PopupPageRenderer popupPageRenderer)
            popupPageRenderer.PopupHandler = this;
        base.ConnectHandler(platformView);
    }

    protected override ContentViewGroup CreatePlatformView()
    {
        return new PopupPageRenderer(Context);
    }


    protected override void DisconnectHandler(ContentViewGroup platformView)
    {
        if (platformView is PopupPageRenderer popupPageRenderer)
            popupPageRenderer.PopupHandler = null;
        base.DisconnectHandler(platformView);
    }
}