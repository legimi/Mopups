﻿using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Mopups.Interfaces;
using Mopups.Pages;
using Mopups.Platforms.MacCatalyst;
using UIKit;
using Application = Microsoft.Maui.Controls.Application;
using VisualElement = Microsoft.Maui.Controls.VisualElement;

namespace Mopups.MacCatalyst.Implementation;

internal class MacOSMopups : IPopupPlatform
{
    // It's necessary because GC in Xamarin.iOS 13 removes all UIWindow if there are not any references to them. See #459
    private readonly List<UIWindow> _windows = new List<UIWindow>();

    private static bool IsiOS9OrNewer => UIDevice.CurrentDevice.CheckSystemVersion(9, 0);

    private static bool IsiOS13OrNewer => UIDevice.CurrentDevice.CheckSystemVersion(13, 0);

    public bool IsSystemAnimationEnabled => true;

    public Task AddAsync(PopupPage page)
    {
        page.Parent = Application.Current.MainPage;

        page.DescendantRemoved += HandleChildRemoved;

        var keyWindow = GetKeyWindow(UIApplication.SharedApplication);
        if (keyWindow?.WindowLevel == UIWindowLevel.Normal)
            keyWindow.WindowLevel = -1;

        var handler = (PageHandler)GetOrCreateMacHandler(page);

        PopupWindow window;

        if (IsiOS13OrNewer)
        {
            UIScene connectedScene = null;

            if (page.Parent is { Handler.MauiContext: not null }) {
                var nativeMainPage = page.Parent.ToPlatform(page.Parent.Handler.MauiContext);
                connectedScene = nativeMainPage.Window.WindowScene;
            }

            if (connectedScene == null) 
                connectedScene = UIApplication.SharedApplication.ConnectedScenes.ToArray().FirstOrDefault(
                    x => x.ActivationState == UISceneActivationState.ForegroundActive);
            if (connectedScene is UIWindowScene windowScene)
                window = new PopupWindow(windowScene);
            else
                window = new PopupWindow();

            _windows.Add(window);
        }
        else
            window = new PopupWindow();

        window.BackgroundColor = UIColor.Clear;
        window.RootViewController = new PopupPageRenderer(handler);

        if (window.RootViewController.View != null)
            window.RootViewController.View.BackgroundColor = UIColor.Clear;

        window.WindowLevel = UIWindowLevel.Normal;
        window.MakeKeyAndVisible();

        handler.ViewController.ModalPresentationStyle = UIKit.UIModalPresentationStyle.OverCurrentContext;
        handler.ViewController.ModalTransitionStyle = UIModalTransitionStyle.CoverVertical;
        

        return window.RootViewController.PresentViewControllerAsync(handler.ViewController, false);

        UIWindow GetKeyWindow(UIApplication application)
        {
            if (!IsiOS13OrNewer)
                return UIApplication.SharedApplication.KeyWindow;

            var window = application
                .ConnectedScenes
                .ToArray()
                .OfType<UIWindowScene>()
                .SelectMany(scene => scene.Windows)
                .FirstOrDefault(window => window.IsKeyWindow);

            return window;
        }
    }

    public async Task RemoveAsync(PopupPage page)
    {
        if (page == null)
            throw new Exception("Popup page is null");

        var handler = page.Handler as PageHandler;
        var viewController = handler?.ViewController;

        await Task.Delay(50);

        page.DescendantRemoved -= HandleChildRemoved;

        if (handler != null && viewController != null && !viewController.IsBeingDismissed)
        {
            var window = viewController.View?.Window as PopupWindow;
            page.Parent = null;

            if (window != null)
            {
                var rvc = window.RootViewController;

                if (rvc != null)
                {
                    await rvc.DismissViewControllerAsync(false);
                    DisposeModelAndChildrenHandlers(page);
                    rvc.Dispose();
                }

                window.RestoreAccessibility();
                window.RootViewController = null;
                window.Hidden = true;

                if (IsiOS13OrNewer && _windows.Contains(window))
                    _windows.Remove(window);

                window.Dispose();
                window = null;
            }

            if (_windows.Count > 0)
                _windows.Last().WindowLevel = UIWindowLevel.Normal;
            else if (UIApplication.SharedApplication.KeyWindow.WindowLevel == -1)
                UIApplication.SharedApplication.KeyWindow.WindowLevel = UIWindowLevel.Normal;
        }
    }

    private static void DisposeModelAndChildrenHandlers(VisualElement view)
    {
        foreach (VisualElement child in view.GetVisualTreeDescendants())
        {
            IViewHandler handler = child.Handler;
            child?.Handler?.DisconnectHandler();
            (handler?.PlatformView as UIView)?.RemoveFromSuperview();
            (handler?.PlatformView as UIView)?.Dispose();
        }

        view?.Handler?.DisconnectHandler();
        (view?.Handler?.PlatformView as UIView)?.RemoveFromSuperview();
        (view?.Handler?.PlatformView as UIView)?.Dispose();
    }

    private void HandleChildRemoved(object sender, ElementEventArgs e)
    {
        var view = e.Element;
        DisposeModelAndChildrenHandlers((VisualElement)view);
    }

    private static IViewHandler GetOrCreateMacHandler(VisualElement bindable)
    {
        if (bindable.Handler != null)
            return bindable.Handler;

        var mauiContext = Application.Current?.Windows?.FirstOrDefault()?.Handler?.MauiContext;

        if (mauiContext != null)
        {
            var handlerType = mauiContext.Handlers.GetHandlerType(bindable.GetType());
            if (handlerType != null)
            {
                var handler = (IViewHandler)Activator.CreateInstance(handlerType);
                bindable.Handler = handler;
                return handler;
            }
        }

        return bindable.Handler ??= new PopupPageHandler();
    }
}