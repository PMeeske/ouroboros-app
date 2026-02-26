// <copyright file="ApiResponseAssertions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Infrastructure.Assertions;

/// <summary>
/// Custom FluentAssertions extensions for ApiResponse{T}.
/// </summary>
public static class ApiResponseAssertionsExtensions
{
    /// <summary>
    /// Returns an ApiResponseAssertionsContext for the given ApiResponse.
    /// </summary>
    public static ApiResponseAssertionsContext<T> Should<T>(this ApiResponse<T> response)
        => new ApiResponseAssertionsContext<T>(response);
}