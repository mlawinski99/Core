namespace Core.IntegrationTests.Shared.Infrastructure;

public static class KeycloakTestUsersData
{
    public const string TestUserId = "550e8400-e29b-41d4-a716-446655440001";
    public const string TestUsername = "testuser";
    public const string TestEmail = "testuser@test.com";
    public const string TestPassword = "testpassword";
    public const string TestUserDeleteId = "11111111-1111-1111-1111-111111111111";
    public const string TestUserUpdateId = "33333333-3333-3333-3333-333333333333";
    public const string TestUserImportId = "44444444-4444-4444-4444-444444444444";

    // only for tests (no email - error on db insert)
    public const string TestUserNoEmailId = "22222222-2222-2222-2222-222222222222";
}
