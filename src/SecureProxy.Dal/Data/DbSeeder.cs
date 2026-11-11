using SecureProxy.Dal.Domain;

namespace SecureProxy.Dal.Data;

public static class DbSeeder
{
    public static void Seed(ProxyDbContext db)
    {
        if (db.Operations.Any())
            return;

        var profile = new EncryptionProfile
        {
            Name = "customer-aes",
            Algorithm = "AES-GCM",
            KeyRef = "config:Customer"
        };

        var destination = new Destination
        {
            Name = "jsonplaceholder",
            BaseUrl = "https://jsonplaceholder.typicode.com",
            IsActive = true,
            TimeoutMs = 10_000
        };

        var getUser = new Operation
        {
            OperationKey = "customer.get",
            Destination = destination,
            HttpMethod = "GET",
            RelativePathTemplate = "/users/{urlId}",
            IsActive = true,
            MaxRequestBytes = 64 * 1024,
            MaxResponseBytes = 1024 * 1024,
            EncryptionRequired = false,
            RejectUnknownParameters = true,
            ParameterRules =
            {
                new ParameterRule
                {
                    Name = "urlId",
                    Required = true,
                    DataType = ParameterDataType.Integer,
                    Location = ParameterLocation.Path,
                    MinValue = 1,
                    MaxValue = 100000
                }
            },
            HeaderRules =
            {
                new HeaderRule { HeaderName = "Accept-Language" }
            }
        };

        var createPost = new Operation
        {
            OperationKey = "post.create",
            Destination = destination,
            HttpMethod = "POST",
            RelativePathTemplate = "/posts",
            IsActive = true,
            MaxRequestBytes = 64 * 1024,
            MaxResponseBytes = 1024 * 1024,
            EncryptionRequired = true,
            EncryptionProfile = profile,
            RejectUnknownParameters = true,
            ParameterRules =
            {
                new ParameterRule
                {
                    Name = "title", Required = true,
                    DataType = ParameterDataType.String, Location = ParameterLocation.Body,
                    MaxLength = 200
                },
                new ParameterRule
                {
                    Name = "body", Required = false,
                    DataType = ParameterDataType.String, Location = ParameterLocation.Body,
                    MaxLength = 2000
                }
            }
        };

        db.EncryptionProfiles.Add(profile);
        db.Destinations.Add(destination);
        db.Operations.AddRange(getUser, createPost);
        db.SaveChanges();
    }
}
