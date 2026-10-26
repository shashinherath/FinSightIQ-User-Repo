namespace FinSightIQ.UserService.Core.DTOs;

/// <summary>
/// Roles defining user permissions within FinSightIQ.
/// </summary>
public enum UserRole
{
    /// <summary>Read market data and predictions.</summary>
    Viewer = 1,

    /// <summary>Viewer permissions plus portfolios, alerts, and prediction requests.</summary>
    Analyst = 2,

    /// <summary>Analyst permissions plus user administration and model management.</summary>
    Administrator = 3
}
