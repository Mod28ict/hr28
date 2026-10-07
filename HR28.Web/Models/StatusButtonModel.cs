namespace HR28.Web.Models;

/// <summary>A voter's support status pill; clickable for people with "Change support status".</summary>
public record StatusButtonModel(Guid VoterId, string VoterName, string? Status);
