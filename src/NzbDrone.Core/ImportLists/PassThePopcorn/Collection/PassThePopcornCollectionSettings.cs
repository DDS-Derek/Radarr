using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.ImportLists.PassThePopcorn.Collection;

public class PassThePopcornCollectionSettingsValidator : AbstractValidator<PassThePopcornCollectionSettings>
{
    public PassThePopcornCollectionSettingsValidator()
    {
        RuleFor(c => c.CollectionUrl).NotEmpty();
        RuleFor(c => c.CollectionUrl).IsValidUrl();
        RuleFor(c => c.ApiUser).NotEmpty();
        RuleFor(c => c.ApiKey).NotEmpty();
    }
}

public class PassThePopcornCollectionSettings : IProviderConfig
{
    private static readonly PassThePopcornCollectionSettingsValidator Validator = new ();

    public PassThePopcornCollectionSettings()
    {
        CollectionUrl = "https://passthepopcorn.me/collages.php?id=21";
    }

    [FieldDefinition(0, Label = "Collection URL", HelpText = "Provide a fully URL to your wanted collection, including filters to your liking.", HelpTextWarning = "Only the first 15 pages will be fetched.", HelpLink = "https://passthepopcorn.me/collages.php")]
    public string CollectionUrl { get; set; }

    [FieldDefinition(1, Label = "API User", HelpText = "These settings are found in your PassThePopcorn security settings (Edit Profile > Security).", Privacy = PrivacyLevel.UserName)]
    public string ApiUser { get; set; }

    [FieldDefinition(2, Label = "API Key", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
    public string ApiKey { get; set; }

    public NzbDroneValidationResult Validate()
    {
        return new NzbDroneValidationResult(Validator.Validate(this));
    }
}
