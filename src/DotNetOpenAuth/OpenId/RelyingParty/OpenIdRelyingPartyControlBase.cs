﻿//-----------------------------------------------------------------------
// <copyright file="OpenIdRelyingPartyControlBase.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

[assembly: System.Web.UI.WebResource(DotNetOpenAuth.OpenId.RelyingParty.OpenIdRelyingPartyControlBase.EmbeddedJavascriptResource, "text/javascript")]

namespace DotNetOpenAuth.OpenId.RelyingParty {
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics.CodeAnalysis;
	using System.Diagnostics.Contracts;
	using System.Drawing.Design;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Web;
	using System.Web.Security;
	using System.Web.UI;
	using DotNetOpenAuth.ComponentModel;
	using DotNetOpenAuth.Configuration;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.OpenId.Extensions;
	using DotNetOpenAuth.OpenId.Extensions.UI;

	/// <summary>
	/// Methods of indicating to the rest of the web site that the user has logged in.
	/// </summary>
	[SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "OnSite", Justification = "Two words intended.")]
	public enum LogOnSiteNotification {
		/// <summary>
		/// The rest of the web site is unaware that the user just completed an OpenID login.
		/// </summary>
		None,

		/// <summary>
		/// After the <see cref="OpenIdRelyingPartyControlBase.LoggedIn"/> event is fired
		/// the control automatically calls <see cref="System.Web.Security.FormsAuthentication.RedirectFromLoginPage(string, bool)"/>
		/// with the <see cref="IAuthenticationResponse.ClaimedIdentifier"/> as the username
		/// unless the <see cref="OpenIdRelyingPartyControlBase.LoggedIn"/> event handler sets
		/// <see cref="OpenIdEventArgs.Cancel"/> property to true.
		/// </summary>
		FormsAuthentication,
	}

	/// <summary>
	/// How an OpenID user session should be persisted across visits.
	/// </summary>
	public enum LogOnPersistence {
		/// <summary>
		/// The user should only be logged in as long as the browser window remains open.
		/// Nothing is persisted to help the user on a return visit.  Public kiosk mode.
		/// </summary>
		Session,

		/// <summary>
		/// The user should only be logged in as long as the browser window remains open.
		/// The OpenID Identifier is persisted to help expedite re-authentication when
		/// the user visits the next time.
		/// </summary>
		SessionAndPersistentIdentifier,

		/// <summary>
		/// The user is issued a persistent authentication ticket so that no login is
		/// necessary on their return visit.
		/// </summary>
		PersistentAuthentication,
	}

	/// <summary>
	/// A common base class for OpenID Relying Party controls.
	/// </summary>
	[DefaultProperty("Identifier"), ValidationProperty("Identifier")]
	public abstract class OpenIdRelyingPartyControlBase : Control, IDisposable {
		/// <summary>
		/// The manifest resource name of the javascript file to include on the hosting page.
		/// </summary>
		internal const string EmbeddedJavascriptResource = Util.DefaultNamespace + ".OpenId.RelyingParty.OpenIdRelyingPartyControlBase.js";

		/// <summary>
		/// The cookie used to persist the Identifier the user logged in with.
		/// </summary>
		internal const string PersistentIdentifierCookieName = OpenIdUtilities.CustomParameterPrefix + "OpenIDIdentifier";

		/// <summary>
		/// The callback parameter name to use to store which control initiated the auth request.
		/// </summary>
		internal const string ReturnToReceivingControlId = OpenIdUtilities.CustomParameterPrefix + "receiver";

		#region Property category constants

		/// <summary>
		/// The "Appearance" category for properties.
		/// </summary>
		protected const string AppearanceCategory = "Appearance";

		/// <summary>
		/// The "Behavior" category for properties.
		/// </summary>
		protected const string BehaviorCategory = "Behavior";

		/// <summary>
		/// The "OpenID" category for properties and events.
		/// </summary>
		protected const string OpenIdCategory = "OpenID";

		#endregion

		#region Callback parameter names

		/// <summary>
		/// The callback parameter to use for recognizing when the callback is in a popup window or hidden iframe.
		/// </summary>
		protected const string UIPopupCallbackKey = OpenIdUtilities.CustomParameterPrefix + "uipopup";

		/// <summary>
		/// The parameter name to include in the formulated auth request so that javascript can know whether
		/// the OP advertises support for the UI extension.
		/// </summary>
		protected const string PopupUISupportedJSHint = OpenIdUtilities.CustomParameterPrefix + "popupUISupported";

		/// <summary>
		/// The callback parameter for use with persisting the <see cref="UsePersistentCookie"/> property.
		/// </summary>
		private const string UsePersistentCookieCallbackKey = OpenIdUtilities.CustomParameterPrefix + "UsePersistentCookie";

		/// <summary>
		/// The callback parameter to use for recognizing when the callback is in the parent window.
		/// </summary>
		private const string UIPopupCallbackParentKey = OpenIdUtilities.CustomParameterPrefix + "uipopupParent";

		#endregion

		#region Property default values

		/// <summary>
		/// The default value for the <see cref="Stateless"/> property.
		/// </summary>
		private const bool StatelessDefault = false;

		/// <summary>
		/// The default value for the <see cref="ReturnToUrl"/> property.
		/// </summary>
		private const string ReturnToUrlDefault = "";

		/// <summary>
		/// Default value of <see cref="UsePersistentCookie"/>.
		/// </summary>
		private const LogOnPersistence UsePersistentCookieDefault = LogOnPersistence.Session;

		/// <summary>
		/// Default value of <see cref="LogOnMode"/>.
		/// </summary>
		private const LogOnSiteNotification LogOnModeDefault = LogOnSiteNotification.FormsAuthentication;

		/// <summary>
		/// The default value for the <see cref="RealmUrl"/> property.
		/// </summary>
		private const string RealmUrlDefault = "~/";

		/// <summary>
		/// The default value for the <see cref="Popup"/> property.
		/// </summary>
		private const PopupBehavior PopupDefault = PopupBehavior.Never;

		/// <summary>
		/// The default value for the <see cref="RequireSsl"/> property.
		/// </summary>
		private const bool RequireSslDefault = false;

		#endregion

		#region Property view state keys

		/// <summary>
		/// The viewstate key to use for the <see cref="Stateless"/> property.
		/// </summary>
		private const string StatelessViewStateKey = "Stateless";

		/// <summary>
		/// The viewstate key to use for the <see cref="UsePersistentCookie"/> property.
		/// </summary>
		private const string UsePersistentCookieViewStateKey = "UsePersistentCookie";

		/// <summary>
		/// The viewstate key to use for the <see cref="LogOnMode"/> property.
		/// </summary>
		private const string LogOnModeViewStateKey = "LogOnMode";

		/// <summary>
		/// The viewstate key to use for the <see cref="RealmUrl"/> property.
		/// </summary>
		private const string RealmUrlViewStateKey = "RealmUrl";

		/// <summary>
		/// The viewstate key to use for the <see cref="ReturnToUrl"/> property.
		/// </summary>
		private const string ReturnToUrlViewStateKey = "ReturnToUrl";

		/// <summary>
		/// The key under which the value for the <see cref="Identifier"/> property will be stored.
		/// </summary>
		private const string IdentifierViewStateKey = "Identifier";

		/// <summary>
		/// The viewstate key to use for the <see cref="Popup"/> property.
		/// </summary>
		private const string PopupViewStateKey = "Popup";

		/// <summary>
		/// The viewstate key to use for the <see cref="RequireSsl"/> property.
		/// </summary>
		private const string RequireSslViewStateKey = "RequireSsl";

		#endregion

		/// <summary>
		/// The lifetime of the cookie used to persist the Identifier the user logged in with.
		/// </summary>
		private static readonly TimeSpan PersistentIdentifierTimeToLiveDefault = TimeSpan.FromDays(14);

		/// <summary>
		/// Backing field for the <see cref="RelyingParty"/> property.
		/// </summary>
		private OpenIdRelyingParty relyingParty;

		/// <summary>
		/// A value indicating whether the <see cref="relyingParty"/> field contains
		/// an instance that we own and should Dispose.
		/// </summary>
		private bool relyingPartyOwned;

		/// <summary>
		/// Initializes a new instance of the <see cref="OpenIdRelyingPartyControlBase"/> class.
		/// </summary>
		protected OpenIdRelyingPartyControlBase() {
		}

		#region Events

		/// <summary>
		/// Fired when the user has typed in their identifier, discovery was successful
		/// and a login attempt is about to begin.
		/// </summary>
		[Description("Fired when the user has typed in their identifier, discovery was successful and a login attempt is about to begin."), Category(OpenIdCategory)]
		public event EventHandler<OpenIdEventArgs> LoggingIn;

		/// <summary>
		/// Fired upon completion of a successful login.
		/// </summary>
		[Description("Fired upon completion of a successful login."), Category(OpenIdCategory)]
		public event EventHandler<OpenIdEventArgs> LoggedIn;

		/// <summary>
		/// Fired when a login attempt fails.
		/// </summary>
		[Description("Fired when a login attempt fails."), Category(OpenIdCategory)]
		public event EventHandler<OpenIdEventArgs> Failed;

		/// <summary>
		/// Fired when an authentication attempt is canceled at the OpenID Provider.
		/// </summary>
		[Description("Fired when an authentication attempt is canceled at the OpenID Provider."), Category(OpenIdCategory)]
		public event EventHandler<OpenIdEventArgs> Canceled;

		/// <summary>
		/// Occurs when the <see cref="Identifier"/> property is changed.
		/// </summary>
		protected event EventHandler IdentifierChanged;

		#endregion

		/// <summary>
		/// Gets or sets the <see cref="OpenIdRelyingParty"/> instance to use.
		/// </summary>
		/// <value>The default value is an <see cref="OpenIdRelyingParty"/> instance initialized according to the web.config file.</value>
		/// <remarks>
		/// A performance optimization would be to store off the 
		/// instance as a static member in your web site and set it
		/// to this property in your <see cref="Control.Load">Page.Load</see>
		/// event since instantiating these instances can be expensive on 
		/// heavily trafficked web pages.
		/// </remarks>
		[Browsable(false)]
		public OpenIdRelyingParty RelyingParty {
			get {
				if (this.relyingParty == null) {
					this.relyingParty = this.CreateRelyingParty();
					this.relyingPartyOwned = true;
				}
				return this.relyingParty;
			}

			set {
				if (this.relyingPartyOwned && this.relyingParty != null) {
					this.relyingParty.Dispose();
				}

				this.relyingParty = value;
				this.relyingPartyOwned = false;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether stateless mode is used.
		/// </summary>
		[Bindable(true), DefaultValue(StatelessDefault), Category(OpenIdCategory)]
		[Description("Controls whether stateless mode is used.")]
		public bool Stateless {
			get { return (bool)(ViewState[StatelessViewStateKey] ?? StatelessDefault); }
			set { ViewState[StatelessViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets the OpenID <see cref="Realm"/> of the relying party web site.
		/// </summary>
		[SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.Uri", Justification = "Using Uri.ctor for validation.")]
		[SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "DotNetOpenAuth.OpenId.Realm", Justification = "Using ctor for validation.")]
		[SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings", Justification = "Bindable property must be simple type")]
		[Bindable(true), DefaultValue(RealmUrlDefault), Category(OpenIdCategory)]
		[Description("The OpenID Realm of the relying party web site.")]
		[UrlProperty, Editor("System.Web.UI.Design.UrlEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(UITypeEditor))]
		public string RealmUrl {
			get {
				return (string)(ViewState[RealmUrlViewStateKey] ?? RealmUrlDefault);
			}

			set {
				if (Page != null && !DesignMode) {
					// Validate new value by trying to construct a Realm object based on it.
					new Realm(OpenIdUtilities.GetResolvedRealm(this.Page, value, this.RelyingParty.Channel.GetRequestFromContext())); // throws an exception on failure.
				} else {
					// We can't fully test it, but it should start with either ~/ or a protocol.
					if (Regex.IsMatch(value, @"^https?://")) {
						new Uri(value.Replace("*.", string.Empty)); // make sure it's fully-qualified, but ignore wildcards
					} else if (value.StartsWith("~/", StringComparison.Ordinal)) {
						// this is valid too
					} else {
						throw new UriFormatException();
					}
				}
				ViewState[RealmUrlViewStateKey] = value;
			}
		}

		/// <summary>
		/// Gets or sets the OpenID ReturnTo of the relying party web site.
		/// </summary>
		[SuppressMessage("Microsoft.Usage", "CA2234:PassSystemUriObjectsInsteadOfStrings", Justification = "Bindable property must be simple type")]
		[SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.Uri", Justification = "Using Uri.ctor for validation.")]
		[SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings", Justification = "Bindable property must be simple type")]
		[Bindable(true), DefaultValue(ReturnToUrlDefault), Category(OpenIdCategory)]
		[Description("The OpenID ReturnTo of the relying party web site.")]
		[UrlProperty, Editor("System.Web.UI.Design.UrlEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(UITypeEditor))]
		public string ReturnToUrl {
			get {
				return (string)(this.ViewState[ReturnToUrlViewStateKey] ?? ReturnToUrlDefault);
			}

			set {
				if (this.Page != null && !this.DesignMode) {
					// Validate new value by trying to construct a Uri based on it.
					new Uri(this.RelyingParty.Channel.GetRequestFromContext().UrlBeforeRewriting, this.Page.ResolveUrl(value)); // throws an exception on failure.
				} else {
					// We can't fully test it, but it should start with either ~/ or a protocol.
					if (Regex.IsMatch(value, @"^https?://")) {
						new Uri(value); // make sure it's fully-qualified, but ignore wildcards
					} else if (value.StartsWith("~/", StringComparison.Ordinal)) {
						// this is valid too
					} else {
						throw new UriFormatException();
					}
				}

				this.ViewState[ReturnToUrlViewStateKey] = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether to send a persistent cookie upon successful 
		/// login so the user does not have to log in upon returning to this site.
		/// </summary>
		[Bindable(true), DefaultValue(UsePersistentCookieDefault), Category(BehaviorCategory)]
		[Description("Whether to send a persistent cookie upon successful " +
			"login so the user does not have to log in upon returning to this site.")]
		public virtual LogOnPersistence UsePersistentCookie {
			get { return (LogOnPersistence)(this.ViewState[UsePersistentCookieViewStateKey] ?? UsePersistentCookieDefault); }
			set { this.ViewState[UsePersistentCookieViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets the way a completed login is communicated to the rest of the web site.
		/// </summary>
		[Bindable(true), DefaultValue(LogOnModeDefault), Category(BehaviorCategory)]
		[Description("The way a completed login is communicated to the rest of the web site.")]
		public virtual LogOnSiteNotification LogOnMode {
			get { return (LogOnSiteNotification)(this.ViewState[LogOnModeViewStateKey] ?? LogOnModeDefault); }
			set { this.ViewState[LogOnModeViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating when to use a popup window to complete the login experience.
		/// </summary>
		/// <value>The default value is <see cref="PopupBehavior.Never"/>.</value>
		[Bindable(true), DefaultValue(PopupDefault), Category(BehaviorCategory)]
		[Description("When to use a popup window to complete the login experience.")]
		public virtual PopupBehavior Popup {
			get { return (PopupBehavior)(ViewState[PopupViewStateKey] ?? PopupDefault); }
			set { ViewState[PopupViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether to enforce on high security mode,
		/// which requires the full authentication pipeline to be protected by SSL.
		/// </summary>
		[Bindable(true), DefaultValue(RequireSslDefault), Category(OpenIdCategory)]
		[Description("Turns on high security mode, requiring the full authentication pipeline to be protected by SSL.")]
		public bool RequireSsl {
			get { return (bool)(ViewState[RequireSslViewStateKey] ?? RequireSslDefault); }
			set { ViewState[RequireSslViewStateKey] = value; }
		}

		/// <summary>
		/// Gets or sets the URL to your privacy policy page that describes how 
		/// claims will be used and/or shared.
		/// </summary>
		[Bindable(true), Category(OpenIdCategory)]
		[Description("The OpenID Identifier that this button will use to initiate login.")]
		[TypeConverter(typeof(IdentifierConverter))]
		public virtual Identifier Identifier {
			get {
				return (Identifier)ViewState[IdentifierViewStateKey];
			}

			set {
				ViewState[IdentifierViewStateKey] = value;
				this.OnIdentifierChanged();
			}
		}

		/// <summary>
		/// Gets or sets the default association preference to set on authentication requests.
		/// </summary>
		internal AssociationPreference AssociationPreference { get; set; }

		/// <summary>
		/// Clears any cookie set by this control to help the user on a returning visit next time.
		/// </summary>
		public static void LogOff() {
			HttpContext.Current.Response.SetCookie(CreateIdentifierPersistingCookie(null));
		}

		/// <summary>
		/// Immediately redirects to the OpenID Provider to verify the Identifier
		/// provided in the text box.
		/// </summary>
		public void LogOn() {
			IAuthenticationRequest request = this.CreateRequests().FirstOrDefault();
			ErrorUtilities.VerifyProtocol(request != null, OpenIdStrings.OpenIdEndpointNotFound);
			this.LogOn(request);
		}

		/// <summary>
		/// Immediately redirects to the OpenID Provider to verify the Identifier
		/// provided in the text box.
		/// </summary>
		/// <param name="request">The request.</param>
		public void LogOn(IAuthenticationRequest request) {
			Contract.Requires(request != null);
			ErrorUtilities.VerifyArgumentNotNull(request, "request");

			if (this.IsPopupAppropriate(request)) {
				this.ScriptPopupWindow(request);
			} else {
				request.RedirectToProvider();
			}
		}

		/// <summary>
		/// Enables a server control to perform final clean up before it is released from memory.
		/// </summary>
		[SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification = "Base class doesn't implement virtual Dispose(bool), so we must call its Dispose() method.")]
		public sealed override void Dispose() {
			this.Dispose(true);
			base.Dispose();
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Releases unmanaged and - optionally - managed resources
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing) {
			if (disposing) {
				if (this.relyingPartyOwned && this.relyingParty != null) {
					this.relyingParty.Dispose();
					this.relyingParty = null;
				}
			}
		}

		/// <summary>
		/// Creates the authentication requests for a given user-supplied Identifier.
		/// </summary>
		/// <returns>A sequence of authentication requests, any one of which may be 
		/// used to determine the user's control of the <see cref="IAuthenticationRequest.ClaimedIdentifier"/>.</returns>
		protected virtual IEnumerable<IAuthenticationRequest> CreateRequests() {
			Contract.Requires(this.Identifier != null, OpenIdStrings.NoIdentifierSet);
			ErrorUtilities.VerifyOperation(this.Identifier != null, OpenIdStrings.NoIdentifierSet);
			IEnumerable<IAuthenticationRequest> requests;

			// Approximate the returnTo (either based on the customize property or the page URL)
			// so we can use it to help with Realm resolution.
			Uri returnToApproximation = this.ReturnToUrl != null ? new Uri(this.RelyingParty.Channel.GetRequestFromContext().UrlBeforeRewriting, this.ReturnToUrl) : this.Page.Request.Url;

			// Resolve the trust root, and swap out the scheme and port if necessary to match the
			// return_to URL, since this match is required by OpenId, and the consumer app
			// may be using HTTP at some times and HTTPS at others.
			UriBuilder realm = OpenIdUtilities.GetResolvedRealm(this.Page, this.RealmUrl, this.RelyingParty.Channel.GetRequestFromContext());
			realm.Scheme = returnToApproximation.Scheme;
			realm.Port = returnToApproximation.Port;

			// Initiate openid request
			// We use TryParse here to avoid throwing an exception which 
			// might slip through our validator control if it is disabled.
			Realm typedRealm = new Realm(realm);
			if (string.IsNullOrEmpty(this.ReturnToUrl)) {
				requests = this.RelyingParty.CreateRequests(this.Identifier, typedRealm);
			} else {
				// Since the user actually gave us a return_to value,
				// the "approximation" is exactly what we want.
				requests = this.RelyingParty.CreateRequests(this.Identifier, typedRealm, returnToApproximation);
			}

			// Some OPs may be listed multiple times (one with HTTPS and the other with HTTP, for example).
			// Since we're gathering OPs to try one after the other, just take the first choice of each OP
			// and don't try it multiple times.
			requests = requests.Distinct(DuplicateRequestedHostsComparer.Instance);

			// Configure each generated request.
			foreach (var req in requests) {
				if (this.IsPopupAppropriate(req)) {
					// Inform ourselves in return_to that we're in a popup.
					req.SetCallbackArgument(UIPopupCallbackKey, "1");

					if (req.Provider.IsExtensionSupported<UIRequest>()) {
						// Inform the OP that we'll be using a popup window consistent with the UI extension.
						req.AddExtension(new UIRequest());

						// Provide a hint for the client javascript about whether the OP supports the UI extension.
						// This is so the window can be made the correct size for the extension.
						// If the OP doesn't advertise support for the extension, the javascript will use
						// a bigger popup window.
						req.SetCallbackArgument(PopupUISupportedJSHint, "1");
					}
				}

				// Add state that needs to survive across the redirect.
				if (!this.Stateless) {
					req.SetCallbackArgument(UsePersistentCookieCallbackKey, this.UsePersistentCookie.ToString());
					req.SetCallbackArgument(ReturnToReceivingControlId, this.ClientID);
				}

				((AuthenticationRequest)req).AssociationPreference = this.AssociationPreference;
				if (this.OnLoggingIn(req)) {
					yield return req;
				}
			}
		}

		/// <summary>
		/// Raises the <see cref="E:Load"/> event.
		/// </summary>
		/// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
		protected override void OnLoad(EventArgs e) {
			base.OnLoad(e);

			if (Page.IsPostBack) {
				// OpenID responses NEVER come in the form of a postback.
				return;
			}

			if (this.Identifier == null) {
				this.TryPresetIdentifierWithCookie();
			}

			// Take an unreliable sneek peek to see if we're in a popup and an OpenID
			// assertion is coming in.  We shouldn't process assertions in a popup window.
			if (this.Page.Request.QueryString[UIPopupCallbackKey] == "1" && this.Page.Request.QueryString[UIPopupCallbackParentKey] == null) {
				// We're in a popup window.  We need to close it and pass the
				// message back to the parent window for processing.
				this.ScriptClosingPopupOrIFrame();
				return; // don't do any more processing on it now
			}

			// Only sniff for an OpenID response if it is targeted at this control.  Note that
			// Stateless mode causes no receiver to be indicated.
			string receiver = this.Page.Request.QueryString[ReturnToReceivingControlId] ?? this.Page.Request.Form[ReturnToReceivingControlId];
			if (receiver == null || receiver == this.ClientID) {
				var response = this.RelyingParty.GetResponse();
				this.ProcessResponse(response);
			}
		}

		/// <summary>
		/// Called when the <see cref="Identifier"/> property is changed.
		/// </summary>
		protected virtual void OnIdentifierChanged() {
			var identifierChanged = this.IdentifierChanged;
			if (identifierChanged != null) {
				identifierChanged(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Processes the response.
		/// </summary>
		/// <param name="response">The response.</param>
		protected virtual void ProcessResponse(IAuthenticationResponse response) {
			if (response == null) {
				return;
			}
			string persistentString = response.GetUntrustedCallbackArgument(UsePersistentCookieCallbackKey);
			if (persistentString != null) {
				this.UsePersistentCookie = (LogOnPersistence)Enum.Parse(typeof(LogOnPersistence), persistentString);
			}

			switch (response.Status) {
				case AuthenticationStatus.Authenticated:
					this.OnLoggedIn(response);
					break;
				case AuthenticationStatus.Canceled:
					this.OnCanceled(response);
					break;
				case AuthenticationStatus.Failed:
					this.OnFailed(response);
					break;
				case AuthenticationStatus.SetupRequired:
				case AuthenticationStatus.ExtensionsOnly:
				default:
					// The NotApplicable (extension-only assertion) is NOT one that we support
					// in this control because that scenario is primarily interesting to RPs
					// that are asking a specific OP, and it is not user-initiated as this textbox
					// is designed for.
					throw new InvalidOperationException(MessagingStrings.UnexpectedMessageReceivedOfMany);
			}
		}

		/// <summary>
		/// Raises the <see cref="E:System.Web.UI.Control.PreRender"/> event.
		/// </summary>
		/// <param name="e">An <see cref="T:System.EventArgs"/> object that contains the event data.</param>
		protected override void OnPreRender(EventArgs e) {
			base.OnPreRender(e);

			this.Page.ClientScript.RegisterClientScriptResource(typeof(OpenIdRelyingPartyControlBase), EmbeddedJavascriptResource);
		}

		/// <summary>
		/// Fires the <see cref="LoggedIn"/> event.
		/// </summary>
		/// <param name="response">The response.</param>
		protected virtual void OnLoggedIn(IAuthenticationResponse response) {
			Contract.Requires(response != null);
			Contract.Requires(response.Status == AuthenticationStatus.Authenticated);
			ErrorUtilities.VerifyArgumentNotNull(response, "response");
			ErrorUtilities.VerifyInternal(response.Status == AuthenticationStatus.Authenticated, "Firing OnLoggedIn event without an authenticated response.");

			var loggedIn = this.LoggedIn;
			OpenIdEventArgs args = new OpenIdEventArgs(response);
			if (loggedIn != null) {
				loggedIn(this, args);
			}

			if (!args.Cancel) {
				if (this.UsePersistentCookie == LogOnPersistence.SessionAndPersistentIdentifier) {
					Page.Response.SetCookie(CreateIdentifierPersistingCookie(response));
				}

				switch (this.LogOnMode) {
					case LogOnSiteNotification.FormsAuthentication:
						FormsAuthentication.RedirectFromLoginPage(response.ClaimedIdentifier, this.UsePersistentCookie == LogOnPersistence.PersistentAuthentication);
						break;
					case LogOnSiteNotification.None:
					default:
						break;
				}
			}
		}

		/// <summary>
		/// Fires the <see cref="LoggingIn"/> event.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <returns>
		/// Returns whether the login should proceed.  False if some event handler canceled the request.
		/// </returns>
		protected virtual bool OnLoggingIn(IAuthenticationRequest request) {
			Contract.Requires(request != null);
			ErrorUtilities.VerifyArgumentNotNull(request, "request");

			EventHandler<OpenIdEventArgs> loggingIn = this.LoggingIn;

			OpenIdEventArgs args = new OpenIdEventArgs(request);
			if (loggingIn != null) {
				loggingIn(this, args);
			}

			return !args.Cancel;
		}

		/// <summary>
		/// Fires the <see cref="Canceled"/> event.
		/// </summary>
		/// <param name="response">The response.</param>
		protected virtual void OnCanceled(IAuthenticationResponse response) {
			Contract.Requires(response != null);
			Contract.Requires(response.Status == AuthenticationStatus.Canceled);
			ErrorUtilities.VerifyArgumentNotNull(response, "response");
			ErrorUtilities.VerifyInternal(response.Status == AuthenticationStatus.Canceled, "Firing Canceled event for the wrong response type.");

			var canceled = this.Canceled;
			if (canceled != null) {
				canceled(this, new OpenIdEventArgs(response));
			}
		}

		/// <summary>
		/// Fires the <see cref="Failed"/> event.
		/// </summary>
		/// <param name="response">The response.</param>
		protected virtual void OnFailed(IAuthenticationResponse response) {
			Contract.Requires(response != null);
			Contract.Requires(response.Status == AuthenticationStatus.Failed);
			ErrorUtilities.VerifyArgumentNotNull(response, "response");
			ErrorUtilities.VerifyInternal(response.Status == AuthenticationStatus.Failed, "Firing Failed event for the wrong response type.");

			var failed = this.Failed;
			if (failed != null) {
				failed(this, new OpenIdEventArgs(response));
			}
		}

		/// <summary>
		/// Creates the relying party instance used to generate authentication requests.
		/// </summary>
		/// <returns>The instantiated relying party.</returns>
		protected virtual OpenIdRelyingParty CreateRelyingParty() {
			return this.CreateRelyingParty(true);
		}

		/// <summary>
		/// Creates the relying party instance used to generate authentication requests.
		/// </summary>
		/// <param name="verifySignature">
		/// A value indicating whether message protections should be applied to the processed messages.
		/// Use <c>false</c> to postpone verification to a later time without invalidating nonces.
		/// </param>
		/// <returns>The instantiated relying party.</returns>
		protected virtual OpenIdRelyingParty CreateRelyingParty(bool verifySignature) {
			IRelyingPartyApplicationStore store = this.Stateless ? null : DotNetOpenAuthSection.Configuration.OpenId.RelyingParty.ApplicationStore.CreateInstance(OpenIdRelyingParty.HttpApplicationStore);
			var rp = verifySignature ? new OpenIdRelyingParty(store) : OpenIdRelyingParty.CreateNonVerifying();

			// Only set RequireSsl to true, as we don't want to override 
			// a .config setting of true with false.
			if (this.RequireSsl) {
				rp.SecuritySettings.RequireSsl = true;
			}

			return rp;
		}

		/// <summary>
		/// Detects whether a popup window should be used to show the Provider's UI.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <returns>
		/// 	<c>true</c> if a popup should be used; <c>false</c> otherwise.
		/// </returns>
		protected virtual bool IsPopupAppropriate(IAuthenticationRequest request) {
			Contract.Requires(request != null);
			ErrorUtilities.VerifyArgumentNotNull(request, "request");

			switch (this.Popup) {
				case PopupBehavior.Never:
					return false;
				case PopupBehavior.Always:
					return true;
				case PopupBehavior.IfProviderSupported:
					return request.Provider.IsExtensionSupported<UIRequest>();
				default:
					throw ErrorUtilities.ThrowInternal("Unexpected value for Popup property.");
			}
		}

		/// <summary>
		/// Adds attributes to an HTML &lt;A&gt; tag that will be written by the caller using 
		/// <see cref="HtmlTextWriter.RenderBeginTag(HtmlTextWriterTag)"/> after this method.
		/// </summary>
		/// <param name="writer">The HTML writer.</param>
		/// <param name="request">The outgoing authentication request.</param>
		/// <param name="windowStatus">The text to try to display in the status bar on mouse hover.</param>
		protected void RenderOpenIdMessageTransmissionAsAnchorAttributes(HtmlTextWriter writer, IAuthenticationRequest request, string windowStatus) {
			Contract.Requires(writer != null);
			Contract.Requires(request != null);
			ErrorUtilities.VerifyArgumentNotNull(writer, "writer");
			ErrorUtilities.VerifyArgumentNotNull(request, "request");

			// We render a standard HREF attribute for non-javascript browsers.
			writer.AddAttribute(HtmlTextWriterAttribute.Href, request.RedirectingResponse.GetDirectUriRequest(this.RelyingParty.Channel).AbsoluteUri);

			// And for the Javascript ones we do the extra work to use form POST where necessary.
			writer.AddAttribute(HtmlTextWriterAttribute.Onclick, this.CreateGetOrPostAHrefValue(request) + " return false;");

			writer.AddStyleAttribute(HtmlTextWriterStyle.Cursor, "pointer");
			if (!string.IsNullOrEmpty(windowStatus)) {
				writer.AddAttribute("onMouseOver", "window.status = " + MessagingUtilities.GetSafeJavascriptValue(windowStatus));
				writer.AddAttribute("onMouseOut", "window.status = null");
			}
		}

		/// <summary>
		/// Wires the popup window to close itself and pass the authentication result to the parent window.
		/// </summary>
		protected virtual void ScriptClosingPopupOrIFrame() {
			StringBuilder startupScript = new StringBuilder();
			startupScript.AppendLine("window.opener.dnoa_internal.processAuthorizationResult(document.URL);");
			startupScript.AppendLine("window.close();");

			this.Page.ClientScript.RegisterStartupScript(typeof(OpenIdRelyingPartyControlBase), "loginPopupClose", startupScript.ToString(), true);

			// TODO: alternately we should probably take over rendering this page here to avoid
			// a lot of unnecessary work on the server and possible momentary display of the 
			// page in the popup window.
		}

		/// <summary>
		/// Creates the identifier-persisting cookie, either for saving or deleting.
		/// </summary>
		/// <param name="response">The positive authentication response; or <c>null</c> to clear the cookie.</param>
		/// <returns>An persistent cookie.</returns>
		private static HttpCookie CreateIdentifierPersistingCookie(IAuthenticationResponse response) {
			HttpCookie cookie = new HttpCookie(PersistentIdentifierCookieName);
			bool clearingCookie = false;

			// We'll try to store whatever it was the user originally typed in, but fallback
			// to the final claimed_id.
			if (response != null && response.Status == AuthenticationStatus.Authenticated) {
				var positiveResponse = (PositiveAuthenticationResponse)response;

				// We must escape the value because XRIs start with =, and any leading '=' gets dropped (by ASP.NET?)
				cookie.Value = Uri.EscapeDataString(positiveResponse.Endpoint.UserSuppliedIdentifier ?? response.ClaimedIdentifier);
			} else {
				clearingCookie = true;
				cookie.Value = string.Empty;
				if (HttpContext.Current.Request.Browser["supportsEmptyStringInCookieValue"] == "false") {
					cookie.Value = "NoCookie";
				}
			}

			if (clearingCookie) {
				// mark the cookie has having already expired to cause the user agent to delete
				// the old persisted cookie.
				cookie.Expires = DateTime.Now.Subtract(TimeSpan.FromDays(1));
			} else {
				// Make the cookie persistent by setting an expiration date
				cookie.Expires = DateTime.Now + PersistentIdentifierTimeToLiveDefault;
			}

			return cookie;
		}

		/// <summary>
		/// Gets the javascript to executee to redirect or POST an OpenID message to a remote party.
		/// </summary>
		/// <param name="request">The authentication request to send.</param>
		/// <returns>The javascript that should execute.</returns>
		private string CreateGetOrPostAHrefValue(IAuthenticationRequest request) {
			Contract.Requires(request != null);
			ErrorUtilities.VerifyArgumentNotNull(request, "request");

			Uri directUri = request.RedirectingResponse.GetDirectUriRequest(this.RelyingParty.Channel);
			return "window.dnoa_internal.GetOrPost(" + MessagingUtilities.GetSafeJavascriptValue(directUri.AbsoluteUri) + ");";
		}

		/// <summary>
		/// Wires the return page to immediately display a popup window with the Provider in it.
		/// </summary>
		/// <param name="request">The request.</param>
		private void ScriptPopupWindow(IAuthenticationRequest request) {
			Contract.Requires(request != null);
			Contract.Requires(this.RelyingParty != null);

			StringBuilder startupScript = new StringBuilder();

			// Add a callback function that the popup window can call on this, the
			// parent window, to pass back the authentication result.
			startupScript.AppendLine("window.dnoa_internal = new Object();");
			startupScript.AppendLine("window.dnoa_internal.processAuthorizationResult = function(uri) { window.location = uri; };");
			startupScript.AppendLine("window.dnoa_internal.popupWindow = function() {");
			startupScript.AppendFormat(
				@"\tvar openidPopup = {0}",
				UIUtilities.GetWindowPopupScript(this.RelyingParty, request, "openidPopup"));
			startupScript.AppendLine("};");

			this.Page.ClientScript.RegisterClientScriptBlock(this.GetType(), "loginPopup", startupScript.ToString(), true);
		}

		/// <summary>
		/// Tries to preset the <see cref="Identifier"/> property based on a persistent
		/// cookie on the browser.
		/// </summary>
		/// <returns>
		/// A value indicating whether the <see cref="Identifier"/> property was
		/// successfully preset to some non-empty value.
		/// </returns>
		private bool TryPresetIdentifierWithCookie() {
			HttpCookie cookie = this.Page.Request.Cookies[PersistentIdentifierCookieName];
			if (cookie != null) {
				this.Identifier = Uri.UnescapeDataString(cookie.Value);
				return true;
			}

			return false;
		}

		/// <summary>
		/// An authentication request comparer that judges equality solely on the OP endpoint hostname.
		/// </summary>
		private class DuplicateRequestedHostsComparer : IEqualityComparer<IAuthenticationRequest> {
			/// <summary>
			/// The singleton instance of this comparer.
			/// </summary>
			private static IEqualityComparer<IAuthenticationRequest> instance = new DuplicateRequestedHostsComparer();

			/// <summary>
			/// Prevents a default instance of the <see cref="DuplicateRequestedHostsComparer"/> class from being created.
			/// </summary>
			private DuplicateRequestedHostsComparer() {
			}

			/// <summary>
			/// Gets the singleton instance of this comparer.
			/// </summary>
			internal static IEqualityComparer<IAuthenticationRequest> Instance {
				get { return instance; }
			}

			#region IEqualityComparer<IAuthenticationRequest> Members

			/// <summary>
			/// Determines whether the specified objects are equal.
			/// </summary>
			/// <param name="x">The first object of type <paramref name="T"/> to compare.</param>
			/// <param name="y">The second object of type <paramref name="T"/> to compare.</param>
			/// <returns>
			/// true if the specified objects are equal; otherwise, false.
			/// </returns>
			public bool Equals(IAuthenticationRequest x, IAuthenticationRequest y) {
				if (x == null && y == null) {
					return true;
				}

				if (x == null || y == null) {
					return false;
				}

				// We'll distinguish based on the host name only, which
				// admittedly is only a heuristic, but if we remove one that really wasn't a duplicate, well,
				// this multiple OP attempt thing was just a convenience feature anyway.
				return string.Equals(x.Provider.Uri.Host, y.Provider.Uri.Host, StringComparison.OrdinalIgnoreCase);
			}

			/// <summary>
			/// Returns a hash code for the specified object.
			/// </summary>
			/// <param name="obj">The <see cref="T:System.Object"/> for which a hash code is to be returned.</param>
			/// <returns>A hash code for the specified object.</returns>
			/// <exception cref="T:System.ArgumentNullException">
			/// The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.
			/// </exception>
			public int GetHashCode(IAuthenticationRequest obj) {
				return obj.Provider.Uri.Host.GetHashCode();
			}

			#endregion
		}
	}
}
