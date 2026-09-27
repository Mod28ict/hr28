namespace HR28.Infrastructure.Helpers;

public static class AuthorizationCodeGenerator
{
    private const string Characters =
        "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var random = new Random();

        return new string(
            Enumerable.Repeat(Characters, 5)
                .Select(s => s[random.Next(s.Length)])
                .ToArray());
    }
}
