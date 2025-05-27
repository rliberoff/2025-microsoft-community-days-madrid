// Ignore Spelling: inbox
// Ignore Spelling: lastmessage
// Ignore Spelling: pragma

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0040
#pragma warning disable SKEXP0110

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Microsoft.SemanticKernel.Plugins.OpenApi.Extensions;

namespace Demo;

internal class Program
{
    private static readonly IConfiguration Configuration;
    private static readonly ILogger<Program> Logger;
    private static readonly BearerAuthenticationProviderWithCancellationToken? BearerAuthenticationProviderWithCancellationToken;

    private static readonly RestApiParameterFilter RestApiParameterFilter = context =>
    {
        if (@"me_sendMail".Equals(context.Operation.Id, StringComparison.OrdinalIgnoreCase) && @"payload".Equals(context.Parameter.Name, StringComparison.OrdinalIgnoreCase))
        {
            context.Parameter.Schema = TrimPropertiesFromRequestBody(context.Parameter.Schema);
            return context.Parameter;
        }

        return context.Parameter;
    };

    static Program()
    {
        var configurationBuilder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory());

        configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());

        if (Debugger.IsAttached)
        {
            configurationBuilder.AddJsonFile(@"appsettings.debug.json", optional: true, reloadOnChange: true);
        }

        configurationBuilder.AddJsonFile(@"appsettings.json", optional: false, reloadOnChange: true)
                            ////.AddJsonFile($@"appsettings.{buier.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                            .AddJsonFile($@"appsettings.{Environment.UserName}.json", optional: true, reloadOnChange: true)
                            .AddEnvironmentVariables()
                            ;

        Configuration = configurationBuilder.Build();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        Logger = loggerFactory.CreateLogger<Program>();
        var bearerLogger = loggerFactory.CreateLogger<BearerAuthenticationProviderWithCancellationToken>();

        BearerAuthenticationProviderWithCancellationToken = new BearerAuthenticationProviderWithCancellationToken(Configuration, bearerLogger);
    }

    protected Program()
    {
        // This constructor is intentionally left empty.
        // It can be used for dependency injection or other initialization logic if needed.
    }

    private static async Task Main()
    {
        Console.WriteLine("Starting...");
        Console.WriteLine("Loading configuration...");

        var azureCredentials = new ChainedTokenCredential(new DefaultAzureCredential(), new EnvironmentCredential());

        Console.WriteLine("Initializing Semantic Kernel...");

        var kernel = InitializeSemanticKernel(azureCredentials, Configuration);

        await LoadPluginsAsync(kernel);

        Console.WriteLine("Initializing Agents...");

        var agentGroupChat = InitializeSemanticKernelAgentsGroupChat(kernel);

        Console.WriteLine("Ready!");

        await PromptLoopAsync(agentGroupChat);
    }

    private static Kernel InitializeSemanticKernel(TokenCredential credential, IConfiguration configuration)
    {
        var oaiClient = new AzureOpenAIClient(new Uri(configuration[@"AzureOpenAIOptions:Endpoint"]!), credential);

        var kernel = Kernel.CreateBuilder()
                           .AddAzureOpenAIChatCompletion(configuration[@"AzureOpenAIOptions:ChatModelDeploymentName"]!, oaiClient)
                           .Build()
                           ;

        kernel.AutoFunctionInvocationFilters.Add(new ExpectedSchemaFunctionFilter(Logger));

        return kernel;
    }

    private static async Task LoadPluginsAsync(Kernel kernel)
    {
        foreach (var pluginPath in Directory.GetDirectories(Constants.PluginsDirectory))
        {
            var copilotAgentPluginParameters = new CopilotAgentPluginParameters
            {
                FunctionExecutionParameters = new()
                {
                    {
                        "https://graph.microsoft.com/v1.0",
                        new OpenApiFunctionExecutionParameters(authCallback: BearerAuthenticationProviderWithCancellationToken!.AuthenticateRequestAsync, enableDynamicOperationPayload: false, enablePayloadNamespacing: true)
                        {
                            ParameterFilter = RestApiParameterFilter,
                        }
                    },
                },
            };

            var pluginName = Path.GetFileName(pluginPath);
            var manifestPath = Path.GetFullPath(Directory.GetFiles(pluginPath, @"*-apiplugin.json").Single());

            Logger.LogInformation(@"Loading plugin '{PluginName}' from {ManifestPath}...", pluginName, manifestPath);

            await kernel.ImportPluginFromCopilotAgentPluginAsync(pluginName, manifestPath, copilotAgentPluginParameters).ConfigureAwait(false);
        }
    }

    private static AgentGroupChat InitializeSemanticKernelAgentsGroupChat(Kernel kernel)
    {
        const string LastMessage = "lastmessage";
        const string TerminationToken = "yes";

        var chiefOfStaffAgent = new ChatCompletionAgent()
        {
            Name = Constants.Agents.ChiefOfStaff,
            Instructions =
            """
        You are the Chief of Staff Agent, responsible for overseeing and orchestrating AI-powered interactions.
        Your goal is to ensure that user queries are **interpreted accurately** and **routed to the appropriate agent**.

            - When a user provides a prompt, you analyze its intent.
            - You assign the task to the appropriate agent (Contacts, Calendar, or Mail).
            - Once an agent refines the request, you review it to ensure it aligns with the user's original intent.
            - You engage in **back-and-forth iteration** with the specialized agents to ensure **accuracy** and **clarity**.
            - You confirm when an agent's refined request is **ready for execution**.
            
        **Rules:**
            - Always verify the agent's modifications against the original user prompt.
            - Ensure the final request aligns with **API plugin specifications**.
            - Continue iterations until you and the agent reach **agreement**.
        """,
            Kernel = kernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new FunctionChoiceBehaviorOptions
                {
                    AllowStrictSchemaAdherence = true,
                }),
            }),
        };

        var calendarAgent = new ChatCompletionAgent()
        {
            Name = Constants.Agents.Calendar,
            Instructions =
            """
        You are the Calendar Agent, ensuring **calendar-related queries** adhere to the Microsoft Graph Calendar API specifications.
        Your job is to validate and refine calendar queries before execution.
        """,
            Kernel = kernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new FunctionChoiceBehaviorOptions
                {
                    AllowStrictSchemaAdherence = true,
                }),
            }),
        };

        var contactsAgent = new ChatCompletionAgent()
        {
            Name = Constants.Agents.Contacts,
            Instructions =
            """
        You are the Contacts Agent, responsible for ensuring **contact-related queries** conform to the Contacts API specifications.
        Your role is to validate, refine, and optimize queries for retrieving contacts.
        """,
            Kernel = kernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new FunctionChoiceBehaviorOptions
                {
                    AllowStrictSchemaAdherence = true,
                }),
            }),
        };

        var emailAgent = new ChatCompletionAgent()
        {
            Name = Constants.Agents.Email,
            Instructions =
            """
        You are the Email Agent, ensuring **email-related queries** conform to the Microsoft Graph Mail API.
        Your role is to validate, refine, and optimize queries for sending or retrieving emails.
        """,
            Kernel = kernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new FunctionChoiceBehaviorOptions
                {
                    AllowStrictSchemaAdherence = true,
                }),
            }),
        };

        var legalSecretaryAgent = new ChatCompletionAgent()
        {
            Name = Constants.Agents.LegalSecretary,
            Instructions =
            """
        You are the Legal Secretary Agent. Your role is to ensure that responses are:
            - Free from **bank account information, Social Security numbers, or ID numbers**.
            - Written in **proper English** with **clear and professional wording**.

        **Rules:**
            - **When you find any restricted information, redact it immediately.**
            - **When the English text is unclear or incorrect, rewrite it for clarity.**

        **Example Response Format:**
            - **English Response**: (Corrected content here)
        """,
            Kernel = kernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new FunctionChoiceBehaviorOptions
                {
                    AllowStrictSchemaAdherence = true,
                }),
            }),
        };

        var selectionFunction = AgentGroupChat.CreatePromptFunctionForStrategy(
            $$$"""
                Examine the provided RESPONSE and choose the next participant.
                State only the name of the chosen participant without explanation.
                Never choose the participant named in the RESPONSE.

                Choose only from these participants:
                - {{{Constants.Agents.Contacts}}}
                - {{{Constants.Agents.Calendar}}}
                - {{{Constants.Agents.Email}}}
                - {{{Constants.Agents.LegalSecretary}}}
                - {{{Constants.Agents.ChiefOfStaff}}}

                Always follow these rules when choosing the next participant:
                - When RESPONSE is a user input, analyze the message:
                    - When it contains words like **"contact"**, **"phone number"**, **"address book"**, choose {{{Constants.Agents.Contacts}}}.
                    - When it contains words like **"calendar"**, **"meeting"**, **"event"**, choose {{{Constants.Agents.Calendar}}}.
                    - When it contains words like **"email"**, **"inbox"**, **"send mail"**, choose {{{Constants.Agents.Email}}}.
                - When RESPONSE is by a specialized agent (Contacts, Calendar, or Email), the **next step MUST ALWAYS be the {{{Constants.Agents.LegalSecretary}}} **.
                - When RESPONSE is by LegalSecretaryAgent, return to the {{{Constants.Agents.ChiefOfStaff}}}.
                - When the topic is unclear, default to the {{{Constants.Agents.ChiefOfStaff}}}.

                RESPONSE:
                {{$lastmessage}}
            """,
            safeParameterNames: LastMessage);

        var terminationFunction = AgentGroupChat.CreatePromptFunctionForStrategy(
            $$$"""
                Examine the RESPONSE and provide at least 1 suggestion the first pass
                The RESPONSE must have both an English AND French version at the end. 
                Then determine whether the content has been deemed satisfactory.
                If content is satisfactory, respond with a single word without explanation: {{{TerminationToken}}}.
                If specific suggestions are being provided, it is not satisfactory.
                If no correction is suggested, it is satisfactory.

                RESPONSE:
                {{$lastmessage}}
           """,
            safeParameterNames: LastMessage);

        ChatHistoryTruncationReducer historyReducer = new(1);   // Use a history reducer to optimize token usage. Only keep the last message in the history.

        AgentGroupChat chat = new(chiefOfStaffAgent, contactsAgent, calendarAgent, emailAgent, legalSecretaryAgent)
        {
            ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy = new KernelFunctionSelectionStrategy(selectionFunction, kernel)
                {
                    InitialAgent = chiefOfStaffAgent,   // Always start with the Chief of Staff Agent
                    HistoryReducer = historyReducer,    // Optimize token usage using a history reducer
                    HistoryVariableName = LastMessage,  // Set prompt variable for tracking
                    ResultParser = (result) =>
                    {
                        var selectedAgent = result.GetValue<string>() ?? chiefOfStaffAgent.Name;
                        Console.WriteLine($@"🔍 Debug: Selection strategy chose {selectedAgent}");
                        return selectedAgent;
                    },
                },
                TerminationStrategy = new KernelFunctionTerminationStrategy(terminationFunction, kernel)
                {
                    Agents = [chiefOfStaffAgent],       // Evaluate only for Chief of Staff responses
                    HistoryReducer = historyReducer,    // Optimize token usage using a history reducer
                    HistoryVariableName = LastMessage,  // Set prompt variable for tracking
                    MaximumIterations = 5,              // Limit total turns to avoid infinite loops
                    ResultParser = (result) => result.GetValue<string>()?.Contains(TerminationToken, StringComparison.OrdinalIgnoreCase) ?? false,
                },
            },
        };

        return chat;
    }

    private static async Task PromptLoopAsync(AgentGroupChat agentGroupChat)
    {
        while (true)
        {
            Console.WriteLine();
            Console.Write(@"How may I help you? > ");

            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals(@"EXIT", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (input.Equals(@"RESET", StringComparison.OrdinalIgnoreCase))
            {
                await agentGroupChat.ResetAsync();
                Console.WriteLine(@"[Conversation has been reset]");
                continue;
            }

            // Add user input to the chat history
            agentGroupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));
            agentGroupChat.IsComplete = false;

            try
            {
                Console.WriteLine("\n🟡 Debug: Invoking chat...");

                await foreach (var response in agentGroupChat.InvokeAsync())
                {
                    var authorName = response.AuthorName!;

                    Console.WriteLine($"🟢 Debug: {authorName} responded!");
                    Console.WriteLine($"\n{authorName.ToUpperInvariant()}:{Environment.NewLine}{response.Content}");

                    // ✅ Explicitly check if a specialized agent is responding
                    if (authorName.Equals(Constants.Agents.Contacts, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($@"🔍 Debug: {Constants.Agents.Contacts} is processing this request.");
                    }
                    else if (authorName.Equals(Constants.Agents.Calendar, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($@"🔍 Debug: {Constants.Agents.Calendar} is processing this request.");
                    }
                    else if (authorName.Equals(Constants.Agents.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($@"🔍 Debug: {Constants.Agents.Email} is processing this request.");
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"⚠️ Error in chat execution: {exception.Message}");

                if (exception.InnerException != null)
                {
                    Console.WriteLine(exception.InnerException.Message);
                }
            }
        }
    }

    private static void TrimPropertiesFromJsonNode(JsonNode jsonNode)
    {
        if (jsonNode is not JsonObject jsonObject)
        {
            return;
        }

        if (jsonObject.TryGetPropertyValue(Constants.RequiredPropertyName, out var requiredRawValue) && requiredRawValue is JsonArray requiredArray)
        {
            jsonNode[Constants.RequiredPropertyName] = new JsonArray([.. requiredArray.Where(x => x is not null).Select(x => x!.GetValue<string>()).Where(x => !Constants.FieldsToIgnore.Contains(x)).Select(x => JsonValue.Create(x))]);
        }

        if (jsonObject.TryGetPropertyValue(Constants.PropertiesPropertyName, out var propertiesRawValue) && propertiesRawValue is JsonObject propertiesObject)
        {
            var properties = propertiesObject.Where(x => Constants.FieldsToIgnore.Contains(x.Key)).Select(static x => x.Key).ToArray();

            foreach (var property in properties)
            {
                propertiesObject.Remove(property);
            }
        }

        jsonObject.Where(subProperty => subProperty.Value is not null)
                 .ToList()
                 .ForEach(subProperty => TrimPropertiesFromJsonNode(subProperty.Value!));
    }

    private static KernelJsonSchema? TrimPropertiesFromRequestBody(KernelJsonSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        var originalSchema = JsonSerializer.Serialize(schema.RootElement);
        var node = JsonNode.Parse(originalSchema);

        if (node is not JsonObject jsonNode)
        {
            return schema;
        }

        TrimPropertiesFromJsonNode(jsonNode);

        return KernelJsonSchema.Parse(node.ToString());
    }
}

#pragma warning restore SKEXP0110
#pragma warning restore SKEXP0040
#pragma warning restore SKEXP0001
