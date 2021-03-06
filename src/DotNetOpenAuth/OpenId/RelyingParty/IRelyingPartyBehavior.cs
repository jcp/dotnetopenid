﻿//-----------------------------------------------------------------------
// <copyright file="IRelyingPartyBehavior.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.OpenId.RelyingParty {
	/// <summary>
	/// Applies a custom security policy to certain OpenID security settings and behaviors.
	/// </summary>
	/// <remarks>
	/// BEFORE MARKING THIS INTERFACE PUBLIC: it's very important that we shift the methods to be channel-level
	/// rather than facade class level and for the OpenIdChannel to be the one to invoke these methods.
	/// </remarks>
	internal interface IRelyingPartyBehavior {
		/// <summary>
		/// Applies a well known set of security requirements to a default set of security settings.
		/// </summary>
		/// <param name="securitySettings">The security settings to enhance with the requirements of this profile.</param>
		/// <remarks>
		/// Care should be taken to never decrease security when applying a profile.
		/// Profiles should only enhance security requirements to avoid being
		/// incompatible with each other.
		/// </remarks>
		void ApplySecuritySettings(RelyingPartySecuritySettings securitySettings);

		/// <summary>
		/// Called when an authentication request is about to be sent.
		/// </summary>
		/// <param name="request">The request.</param>
		/// <remarks>
		/// Implementations should be prepared to be called multiple times on the same outgoing message
		/// without malfunctioning.
		/// </remarks>
		void OnOutgoingAuthenticationRequest(IAuthenticationRequest request);

		/// <summary>
		/// Called when an incoming positive assertion is received.
		/// </summary>
		/// <param name="assertion">The positive assertion.</param>
		void OnIncomingPositiveAssertion(IAuthenticationResponse assertion);
	}
}
