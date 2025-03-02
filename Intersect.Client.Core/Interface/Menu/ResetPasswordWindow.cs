﻿using System.Security.Cryptography;
using System.Text;
using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Client.Interface.Game.Chat;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Utilities;

namespace Intersect.Client.Interface.Menu;


public partial class ResetPasswordWindow
{

    private Button mBackBtn;

    //Reset Code
    private ImagePanel mCodeInputBackground;

    private Label mCodeInputLabel;

    private TextBox mCodeInputTextbox;

    //Parent
    private MainMenu mMainMenu;

    //Password Fields
    private ImagePanel mPasswordBackground;

    private ImagePanel mPasswordBackground2;

    private Label mPasswordLabel;

    private Label mPasswordLabel2;

    private TextBoxPassword mPasswordTextbox;

    private TextBoxPassword mPasswordTextbox2;

    //Controls
    private ImagePanel mResetWindow;

    private Button mSubmitBtn;

    private Label mWindowHeader;

    //Init
    public ResetPasswordWindow(Canvas parent, MainMenu mainMenu)
    {
        //Assign References
        mMainMenu = mainMenu;

        //Main Menu Window
        mResetWindow = new ImagePanel(parent, "ResetPasswordWindow");
        mResetWindow.IsHidden = true;

        //Menu Header
        mWindowHeader = new Label(mResetWindow, "Header");
        mWindowHeader.SetText(Strings.ResetPass.Title);

        //Code Fields/Labels
        mCodeInputBackground = new ImagePanel(mResetWindow, "CodePanel");

        mCodeInputLabel = new Label(mCodeInputBackground, "CodeLabel");
        mCodeInputLabel.SetText(Strings.ResetPass.Code);

        mCodeInputTextbox = new TextBox(mCodeInputBackground, "CodeField")
        {
            IsTabable = true,
        };
        mCodeInputTextbox.SubmitPressed += Textbox_SubmitPressed;
        mCodeInputTextbox.Clicked += Textbox_Clicked;

        //Password Fields/Labels
        //Register Password Background
        mPasswordBackground = new ImagePanel(mResetWindow, "Password1Panel");

        mPasswordLabel = new Label(mPasswordBackground, "Password1Label");
        mPasswordLabel.SetText(Strings.ResetPass.NewPassword);

        mPasswordTextbox = new TextBoxPassword(mPasswordBackground, "Password1Field")
        {
            IsTabable = true,
        };
        mPasswordTextbox.SubmitPressed += PasswordTextbox_SubmitPressed;

        //Confirm Password Fields/Labels
        mPasswordBackground2 = new ImagePanel(mResetWindow, "Password2Panel");

        mPasswordLabel2 = new Label(mPasswordBackground2, "Password2Label");
        mPasswordLabel2.SetText(Strings.ResetPass.ConfirmPassword);

        mPasswordTextbox2 = new TextBoxPassword(mPasswordBackground2, "Password2Field")
        {
            IsTabable = true,
        };
        mPasswordTextbox2.SubmitPressed += PasswordTextbox2_SubmitPressed;

        //Login - Send Login Button
        mSubmitBtn = new Button(mResetWindow, "SubmitButton")
        {
            IsTabable = true,
        };
        mSubmitBtn.SetText(Strings.ResetPass.Submit);
        mSubmitBtn.Clicked += SubmitBtn_Clicked;

        //Login - Back Button
        mBackBtn = new Button(mResetWindow, "BackButton")
        {
            IsTabable = true,
        };
        mBackBtn.SetText(Strings.ResetPass.Back);
        mBackBtn.Clicked += BackBtn_Clicked;

        mResetWindow.LoadJsonUi(GameContentManager.UI.Menu, Graphics.Renderer.GetResolutionString());
    }

    public bool IsHidden => mResetWindow.IsHidden;

    //The username or email of the acc we are resetting the pass for
    public string Target { set; get; } = string.Empty;

    private void Textbox_Clicked(Base sender, MouseButtonState arguments)
    {
        Globals.InputManager.OpenKeyboard(
            KeyboardType.Normal, mCodeInputTextbox.Text, false, false, false
        );
    }

    //Methods
    public void Update()
    {
        // ReSharper disable once InvertIf
        if (!Networking.Network.IsConnected)
        {
            Hide();
            mMainMenu.Show();
        }
    }

    public void Hide()
    {
        mResetWindow.IsHidden = true;
    }

    public void Show()
    {
        mResetWindow.IsHidden = false;
        mCodeInputTextbox.Text = string.Empty;
        mPasswordTextbox.Text = string.Empty;
        mPasswordTextbox2.Text = string.Empty;
    }

    void BackBtn_Clicked(Base sender, MouseButtonState arguments)
    {
        Hide();
        Interface.MenuUi.MainMenu.NotifyOpenLogin();
    }

    void Textbox_SubmitPressed(Base sender, EventArgs arguments)
    {
        TrySendCode();
    }

    void SubmitBtn_Clicked(Base sender, MouseButtonState arguments)
    {
        TrySendCode();
    }

    void PasswordTextbox_SubmitPressed(Base sender, EventArgs arguments)
    {
        TrySendCode();
    }

    void PasswordTextbox2_SubmitPressed(Base sender, EventArgs arguments)
    {
        TrySendCode();
    }

    public void TrySendCode()
    {
        if (Globals.WaitingOnServer)
        {
            return;
        }

        if (!Networking.Network.IsConnected)
        {
            Interface.ShowAlert(Strings.Errors.NotConnected, alertType: AlertType.Error);
            return;
        }

        if (string.IsNullOrEmpty(mCodeInputTextbox?.Text))
        {
            Interface.ShowAlert(Strings.ResetPass.InputCode, alertType: AlertType.Error);
            return;
        }

        if (mPasswordTextbox.Text != mPasswordTextbox2.Text)
        {
            Interface.ShowAlert(Strings.Registration.PasswordMismatch, alertType: AlertType.Error);
            return;
        }

        if (!FieldChecking.IsValidPassword(mPasswordTextbox.Text, Strings.Regex.Password))
        {
            Interface.ShowAlert(Strings.Errors.PasswordInvalid, alertType: AlertType.Error);
            return;
        }

        using (var sha = new SHA256Managed())
        {
            PacketSender.SendResetPassword(
                Target, mCodeInputTextbox?.Text,
                BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(mPasswordTextbox.Text.Trim())))
                    .Replace("-", "")
            );
        }

        Globals.WaitingOnServer = true;
        ChatboxMsg.ClearMessages();
    }

}
