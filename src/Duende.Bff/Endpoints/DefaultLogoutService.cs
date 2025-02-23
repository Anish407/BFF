﻿// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Duende.Bff;

/// <summary>
/// Service for handling logout requests
/// </summary>
public class DefaultLogoutService : ILogoutService
{
    /// <summary>
    /// The BFF options
    /// </summary>
    protected readonly BffOptions Options;
        
    /// <summary>
    /// The scheme provider
    /// </summary>
    protected readonly IAuthenticationSchemeProvider AuthenticationSchemeProvider;

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="authenticationAuthenticationSchemeProviderProvider"></param>
    public DefaultLogoutService(BffOptions options, IAuthenticationSchemeProvider authenticationAuthenticationSchemeProviderProvider)
    {
        Options = options;
        AuthenticationSchemeProvider = authenticationAuthenticationSchemeProviderProvider;
    }

    /// <inheritdoc />
    public virtual async Task ProcessRequestAsync(HttpContext context)
    {
        context.CheckForBffMiddleware(Options);
            
        var result = await context.AuthenticateAsync();
        if (result.Succeeded && result.Principal?.Identity?.IsAuthenticated == true)
        {
            var userSessionId = result.Principal.FindFirst(JwtClaimTypes.SessionId)?.Value;
            if (!String.IsNullOrWhiteSpace(userSessionId))
            {
                var passedSessionId = context.Request.Query[JwtClaimTypes.SessionId].FirstOrDefault();
                // for an authenticated user, if they have a sesison id claim,
                // we require the logout request to pass that same value to
                // prevent unauthenticated logout requests (similar to OIDC front channel)
                if (Options.RequireLogoutSessionId && userSessionId != passedSessionId)
                {
                    throw new Exception("Invalid Session Id");
                }
            }
        }
            
        // get rid of local cookie first
        var signInScheme = await AuthenticationSchemeProvider.GetDefaultSignInSchemeAsync();
        await context.SignOutAsync(signInScheme?.Name);

        var returnUrl = context.Request.Query[Constants.RequestParameters.ReturnUrl].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            if (!Util.IsLocalUrl(returnUrl))
            {
                throw new Exception("returnUrl is not application local: " + returnUrl);
            }
        }

        if (String.IsNullOrWhiteSpace(returnUrl))
        {
            if (context.Request.PathBase.HasValue)
            {
                returnUrl = context.Request.PathBase;
            }
            else
            {
                returnUrl = "/";
            }
        }
        
        var props = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        // trigger idp logout
        await context.SignOutAsync(props);
    }
}