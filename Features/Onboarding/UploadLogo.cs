using Dapper;
using DammaniAPI.Database;
using DammaniAPI.Features;
using DammaniAPI.Services.Storage;
using MediatR;

namespace DammaniAPI.Features.Onboarding;

public class UploadLogo
{
    public class Command : IRequest<Result>
    {
        public string? ShopId { get; set; }
        public FilePayload? File { get; set; }
    }

    public class FilePayload
    {
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public byte[] Content { get; set; } = [];
    }

    public class Result
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? LogoPath { get; set; }
    }

    public class CommandHandler : IRequestHandler<Command, Result>
    {
        internal const long MaxFileSizeBytes = 2 * 1024 * 1024;

        internal static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["image/png"] = ".png",
                ["image/jpeg"] = ".jpg",
                ["image/jpg"] = ".jpg",
                ["image/svg+xml"] = ".svg"
            };

        internal static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".svg" };

        private readonly IManagementDatabase _mdb;
        private readonly IFileStorage _storage;

        public CommandHandler(IManagementDatabase mdb, IFileStorage storage)
        {
            _mdb = mdb;
            _storage = storage;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ShopId))
                return new Result { Success = false, ErrorCode = ErrorCodes.Unauthorized };

            if (!IsValidFile(request.File))
                return new Result { Success = false, ErrorCode = ErrorCodes.InvalidFiles };

            var file = request.File!;
            var extension = AllowedContentTypes[file.ContentType];
            using var content = new MemoryStream(file.Content);
            var logoPath = await _storage.SaveAsync(content, "logos", extension, ct);

            using var db = _mdb.Open();
            var updated = await db.ExecuteAsync(
                """
                UPDATE Shop
                SET LogoPath = @LogoPath, UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @ShopId
                """,
                new { request.ShopId, LogoPath = logoPath });

            return updated == 0
                ? new Result { Success = false, ErrorCode = ErrorCodes.NotFound }
                : new Result { Success = true, LogoPath = logoPath };
        }

        internal static bool IsValidFile(FilePayload? file)
        {
            if (file == null || file.Content.Length == 0 || file.Content.Length > MaxFileSizeBytes)
                return false;

            if (!AllowedContentTypes.ContainsKey(file.ContentType))
                return false;

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                return false;

            return AllowedContentTypes[file.ContentType].Equals(extension, StringComparison.OrdinalIgnoreCase)
                   || (extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                       && file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase));
        }
    }
}
