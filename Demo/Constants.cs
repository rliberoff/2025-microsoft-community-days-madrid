namespace Demo;

internal static class Constants
{
    internal static readonly string PluginsDirectory = @"Plugins/CopilotAgentPlugins";

    internal static readonly string PropertiesPropertyName = @"properties";

    internal static readonly string RequiredPropertyName = @"required";

    internal static readonly HashSet<string> FieldsToIgnore = new(
    [
        "@odata.type",
        "allowNewTimeProposals",
        "attachments",
        "bccRecipients",
        "bodyPreview",
        "calendar",
        "categories",
        "changeKey",
        "createdDateTime",
        "ccRecipients",
        "conversationId",
        "conversationIndex",
        "extensions",
        "flag",
        "from",
        "hasAttachments",
        "hideAttendees",
        "iCalUId",
        "id",
        "importance",
        "inferenceClassification",
        "instances",
        "internetMessageHeaders",
        "isCancelled",
        "isDeliveryReceiptRequested",
        "isDraft",
        "isOnlineMeeting",
        "isOrganizer",
        "isRead",
        "isReadReceiptRequested",
        "isReminderOn",
        "lastModifiedDateTime",
        "multiValueExtendedProperties",
        "originalEndTimeZone",
        "originalStart",
        "originalStartTimeZone",
        "parentFolderId",
        "pattern",
        "receivedDateTime",
        "recurrence",
        "reminderMinutesBeforeStart",
        "replyTo",
        "responseRequested",
        "responseStatus",
        "sender",
        "sensitivity",
        "sentDateTime",
        "seriesMasterId",
        "singleValueExtendedProperties",
        "transactionId",
        "uniqueBody",
        "webLink",
    ], StringComparer.OrdinalIgnoreCase);

    internal static class Agents
    {
        internal static readonly string Assistant = @"AssistantAgent";

        internal static readonly string Calendar = @"CalendarAgent";

        internal static readonly string Contacts = @"ContactsAgent";

        internal static readonly string Email = @"EmailAgent";

        internal static readonly string LegalAdvisor = @"LegalAdvisorAgent";
    }
}
