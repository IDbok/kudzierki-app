namespace Shared.Primitives;

public static class Errors
{
    public static class Auth
    {
        public static readonly Error InvalidCredentials = new(
            "Auth.InvalidCredentials",
            "The provided email or password is invalid");

        public static readonly Error InvalidToken = new(
            "Auth.InvalidToken",
            "The provided token is invalid");

        public static readonly Error TokenExpired = new(
            "Auth.TokenExpired",
            "The provided token has expired");
    }

    public static class Validation
    {
        public static Error EmptyField(string fieldName) => new(
            "Validation.EmptyField",
            $"The {fieldName} field is required");

        public static Error InvalidFormat(string fieldName) => new(
            "Validation.InvalidFormat",
            $"The {fieldName} field has an invalid format");
    }
}
