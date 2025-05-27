namespace Demo;

internal static class Constants
{
    internal static readonly string PluginsDirectory = @"Plugins/CopilotAgentPlugins";

    internal static readonly string PropertiesPropertyName = @"properties";

    internal static readonly string RequiredPropertyName = @"required";

    internal static readonly HashSet<string> FieldsToIgnore = new(
    [
        "@odata.type",
        "attachments",
        "bccRecipients",
        "bodyPreview",
        "categories",
        "ccRecipients",
        "conversationId",
        "conversationIndex",
        "extensions",
        "flag",
        "from",
        "hasAttachments",
        "id",
        "inferenceClassification",
        "internetMessageHeaders",
        "isDeliveryReceiptRequested",
        "isDraft",
        "isRead",
        "isReadReceiptRequested",
        "multiValueExtendedProperties",
        "parentFolderId",
        "receivedDateTime",
        "replyTo",
        "sender",
        "sentDateTime",
        "singleValueExtendedProperties",
        "uniqueBody",
        "webLink",
    ], StringComparer.OrdinalIgnoreCase);

    internal static class Agents
    {
        internal static readonly string ChiefOfStaff = @"ChiefOfStaffAgent";

        internal static readonly string Calendar = @"CalendarAgent";

        internal static readonly string Contacts = @"ContactsAgent";

        internal static readonly string Email = @"EmailAgent";

        internal static readonly string LegalSecretary = @"LegalSecretaryAgent";
    }
}
