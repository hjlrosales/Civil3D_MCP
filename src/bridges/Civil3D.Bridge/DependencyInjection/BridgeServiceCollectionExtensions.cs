using Autodesk.Mcp.Sdk.Communication;
using Autodesk.Mcp.Sdk.Dispatch;
using Autodesk.Mcp.Sdk.Discovery;
using Autodesk.Mcp.Sdk.Hosting;
using Autodesk.Mcp.Sdk.Registration;
using Autodesk.Mcp.Sdk.Tools;
using Autodesk.Mcp.Shared.Contracts;
using Civil3D.Bridge.Configuration;
using Civil3D.Bridge.Data;
using Civil3D.Bridge.Execution;
using Civil3D.Bridge.Plugin;
using Civil3D.Bridge.Services;
using Civil3D.Domain.Commands;
using Civil3D.Domain.Commands.Transactions;
using Civil3D.Domain.Alignments.Data;
using Civil3D.Domain.Alignments.Repositories;
using Civil3D.Domain.Alignments.Services;
using Civil3D.Domain.Cogo.Data;
using Civil3D.Domain.Cogo.Repositories;
using Civil3D.Domain.Cogo.Services;
using Civil3D.Domain.Corridors.Data;
using Civil3D.Domain.Corridors.Repositories;
using Civil3D.Domain.Corridors.Services;
using Civil3D.Domain.Data;
using Civil3D.Domain.Pipes.Data;
using Civil3D.Domain.Pipes.Dtos;
using Civil3D.Domain.Pipes.Repositories;
using Civil3D.Domain.Pipes.Services;
using Civil3D.Domain.Profiles.Data;
using Civil3D.Domain.Profiles.Repositories;
using Civil3D.Domain.Profiles.Services;
using Civil3D.Domain.Styles.Data;
using Civil3D.Domain.Styles.Repositories;
using Civil3D.Domain.Styles.Services;
using Civil3D.Domain.Surfaces.Data;
using Civil3D.Domain.Surfaces.Repositories;
using Civil3D.Domain.Surfaces.Services;
using Civil3D.Domain.Workflows;
using Civil3D.Tools.Abstractions;
using Civil3D.Tools.Commands;
using Civil3D.Tools.Editing.Commands;
using Civil3D.Tools.Editing.Validators;
using Civil3D.Tools.Drawing.Services;
using Civil3D.Tools.Health.Dtos;
using Civil3D.Tools.Health.Workflow;
using Civil3D.Tools.Project.Dtos;
using Civil3D.Tools.Project.Workflow;
using Civil3D.Tools.Quantity.Dtos;
using Civil3D.Tools.Quantity.Workflow;
using Civil3D.Tools.Surface.Dtos;
using Civil3D.Tools.Surface.Workflow;
using Civil3D.Tools.Corridor.Dtos;
using Civil3D.Tools.Corridor.Workflow;
using Civil3D.Tools.Export.Abstractions;
using Civil3D.Tools.Export.Dtos;
using Civil3D.Tools.Export.Workflow;
using Civil3D.Tools.CutFill.Abstractions;
using Civil3D.Tools.CutFill.Dtos;
using Civil3D.Tools.CutFill.Workflow;
using Civil3D.Tools.Validation.Dtos;
using Civil3D.Tools.Validation.Framework;
using Civil3D.Tools.Validation.Rules;
using Civil3D.Tools.Validation.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Civil3D.Bridge.DependencyInjection;

/// <summary>
/// Composition root for the Civil 3D Bridge. Registers the SDK-owned bridge infrastructure
/// (pipe host, router, handlers, catalog, endpoint registrar, host) plus the bridge-owned
/// services (info provider, application context, tool dispatcher). Constructor injection only.
/// </summary>
public static class BridgeServiceCollectionExtensions
{
    /// <summary>Registers all bridge services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Raw bridge configuration.</param>
    public static IServiceCollection AddCivil3DBridge(this IServiceCollection services, BridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        // Identity / options.
        services.AddSingleton(options);
        services.AddSingleton(new BridgeHostOptions
        {
            BridgeName = options.BridgeName,
            Product = options.Product,
            ProductVersion = options.ProductVersion,
            BridgeVersion = ParseVersion(options.BridgeVersion, new VersionInformation(1, 0, 0)),
            SdkVersion = ParseVersion(options.SdkVersion, new VersionInformation(1, 0, 0)),
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            PipeName = string.IsNullOrWhiteSpace(options.PipeName)
                ? $"{ProtocolConstants.PipeNamePrefix}{options.Product.ToLowerInvariant()}-{Environment.ProcessId}"
                : options.PipeName,
            MaxConcurrentConnections = Math.Max(1, options.MaxConcurrentConnections),
            SupportedProducts = options.SupportedProducts,
        });

        // Hosting primitives.
        services.AddSingleton<BridgeShutdown>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<CancellationRegistry>();

        // Discovery: the SDK scans every (non-dynamic) assembly loaded into the bridge process, so
        // product tool assemblies (Civil3D.Tools.Drawing, and future Civil3D.Tools.* assemblies) are
        // discovered without any per-tool compile-time registration. Referencing the tool services
        // below also forces those assemblies to load before the catalog is constructed.
        services.AddSingleton<ManifestGenerator>();
        services.AddSingleton(sp => new ToolCatalog(
            AppDomain.CurrentDomain.GetAssemblies().Where(static a => !a.IsDynamic).ToArray(),
            sp.GetRequiredService<ManifestGenerator>(),
            sp,
            sp.GetRequiredService<ILogger<ToolCatalog>>()));
        services.AddSingleton<IToolCatalog>(sp => sp.GetRequiredService<ToolCatalog>());

        // Drawing tool services: contracts live in Civil3D.Tools.Abstractions, the real Autodesk
        // implementations live in Civil3D.Tools.Drawing.
        services.AddSingleton<ICivil3DSession, AutodeskCivil3DSession>();
        services.AddSingleton<IDrawingStatisticsService, AutodeskDrawingStatisticsService>();

        // Domain layer: the read-only transaction context plus every discipline's data source,
        // repository and service. Tools (Phase 4B) depend only on the *Service interfaces.
        services.AddSingleton<IAutodeskDocumentContext, AutodeskDocumentContext>();

        services.AddSingleton<IAlignmentDataSource, AutodeskAlignmentDataSource>();
        services.AddSingleton<IAlignmentRepository, AlignmentRepository>();
        services.AddSingleton<IAlignmentService, AlignmentService>();

        services.AddSingleton<ISurfaceDataSource, AutodeskSurfaceDataSource>();
        services.AddSingleton<ISurfaceRepository, SurfaceRepository>();
        services.AddSingleton<ISurfaceService, SurfaceService>();

        services.AddSingleton<IProfileDataSource, AutodeskProfileDataSource>();
        services.AddSingleton<IProfileRepository, ProfileRepository>();
        services.AddSingleton<IProfileService, ProfileService>();

        services.AddSingleton<ICorridorDataSource, AutodeskCorridorDataSource>();
        services.AddSingleton<ICorridorRepository, CorridorRepository>();
        services.AddSingleton<ICorridorService, CorridorService>();

        services.AddSingleton<IPipeDataSource, AutodeskPipeDataSource>();
        services.AddSingleton<IPipeRepository, PipeRepository>();
        services.AddSingleton<IPipeService, PipeService>();

        services.AddSingleton<ICogoDataSource, AutodeskCogoDataSource>();
        services.AddSingleton<ICogoRepository, CogoRepository>();
        services.AddSingleton<ICogoService, CogoService>();

        services.AddSingleton<IStyleDataSource, AutodeskStyleDataSource>();
        services.AddSingleton<IStyleRepository, StyleRepository>();
        services.AddSingleton<IStyleService, StyleService>();

        // Command framework (Phase 5A): the write transaction provider is Autodesk-backed; the
        // dispatcher pipeline, undo context and event dispatcher are infrastructure. Editing
        // commands (handlers/validators) register here (Phase 5B).
        services.AddSingleton<ITransactionProvider, AutodeskTransactionProvider>();
        services.AddSingleton<ITransactionPipeline, TransactionPipeline>();
        services.AddSingleton<IDomainEventDispatcher, InMemoryDomainEventDispatcher>();
        services.AddSingleton<IUndoContext>(_ => NullUndoContext.Instance);
        services.AddSingleton<IConfirmationGate>(_ => NullConfirmationGate.Instance);
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();

        // Editing commands (Phase 5B): rename repositories and services, plus the generic
        // rename handler and structural validators bound to each discipline command.
        services.AddSingleton<IAlignmentRenameRepository, AutodeskAlignmentRenameRepository>();
        services.AddSingleton<IRenameAlignmentService, RenameAlignmentService>();
        services.AddSingleton<ISurfaceRenameRepository, AutodeskSurfaceRenameRepository>();
        services.AddSingleton<IRenameSurfaceService, RenameSurfaceService>();

        services.AddTransient<ICommandHandler<RenameAlignmentCommand, RenameResult>>(sp =>
            new RenameCommandHandler<RenameAlignmentCommand>(
                (transaction, id, newName, context) =>
                    sp.GetRequiredService<IRenameAlignmentService>().Rename(transaction, id, newName, context)));
        services.AddTransient<ICommandHandler<RenameSurfaceCommand, RenameResult>>(sp =>
            new RenameCommandHandler<RenameSurfaceCommand>(
                (transaction, id, newName, context) =>
                    sp.GetRequiredService<IRenameSurfaceService>().Rename(transaction, id, newName, context)));
        services.AddTransient<ICommandValidator<RenameAlignmentCommand>, RenameAlignmentCommandValidator>();
        services.AddTransient<ICommandValidator<RenameSurfaceCommand>, RenameSurfaceCommandValidator>();

        // create_pipe (Phase 5C): the pipe create write repository and service, plus the command
        // handler and structural validator. Reuses the read-only IPipeRepository registered above
        // to confirm the target network exists before any Autodesk write is attempted.
        services.AddSingleton<IPipeCreateRepository, AutodeskPipeCreateRepository>();
        services.AddSingleton<ICreatePipeService, CreatePipeService>();
        services.AddTransient<ICommandHandler<CreatePipeCommand, CreatePipeResult>, CreatePipeCommandHandler>();
        services.AddTransient<ICommandValidator<CreatePipeCommand>, CreatePipeCommandValidator>();

        // Workflow framework (Phase 7A): the dispatcher pipeline, progress, timeout/cancellation
        // and events are infrastructure; handlers and validators for engineering workflows
        // register here like the editing commands above.
        services.AddSingleton<IWorkflowDispatcher, WorkflowDispatcher>();

        // Drawing health workflow (Phase 7B): the first production engineering workflow. The
        // handler runs the collection/analysis steps through the dispatcher; the tool is
        // discovered automatically by assembly scanning like every other Civil3D.Tools.* tool.
        services.AddSingleton<IWorkflowHandler<DrawingHealthWorkflow, DrawingHealthReport>,
            DrawingHealthWorkflowHandler>();

        // Project summary workflow (Phase 7C): the read-only project overview workflow. Same
        // pattern — handler registered here, tool discovered by assembly scanning.
        services.AddSingleton<IWorkflowHandler<ProjectSummaryWorkflow, ProjectSummaryReport>,
            ProjectSummaryWorkflowHandler>();

        // Design validation framework (Phase 7D): the reusable validation engine discovers its
        // rules through the container, so new rules compose without engine changes. The
        // design_validation_report workflow handler and tool follow the Phase 7B/7C pattern.
        services.AddSingleton<IValidationEngine, ValidationEngine>();
        services.AddSingleton<IValidationRule, DuplicateNameRule>();
        services.AddSingleton<IValidationRule, MissingDescriptionRule>();
        services.AddSingleton<IValidationRule, EmptyCollectionRule>();
        services.AddSingleton<IValidationRule, UnresolvedReferenceRule>();
        services.AddSingleton<IValidationRule, UnusedStyleRule>();
        services.AddSingleton<IValidationRule, DuplicateCogoPointNumberRule>();
        services.AddSingleton<IValidationRule, ProfileWithoutAlignmentRule>();
        services.AddSingleton<IValidationRule, PipeNetworkWithoutStructureRule>();
        services.AddSingleton<IWorkflowHandler<DesignValidationWorkflow, DesignValidationReport>,
            DesignValidationWorkflowHandler>();

        // Quantity takeoff workflow (Phase 7E): the read-only quantity report workflow. Same
        // pattern — handler registered here, tool discovered by assembly scanning.
        services.AddSingleton<IWorkflowHandler<QuantityTakeoffWorkflow, QuantityTakeoffReport>,
            QuantityTakeoffWorkflowHandler>();

        // Surface comparison workflow (Phase 7F): the read-only surface_comparison_report
        // workflow. Same pattern — handler registered here, tool discovered by assembly scanning.
        services.AddSingleton<IWorkflowHandler<SurfaceComparisonWorkflow, SurfaceComparisonReport>,
            SurfaceComparisonWorkflowHandler>();

        // Cut/fill workflow (Phase 7G): the first geometry-aware engineering workflow. The
        // workflow depends only on the ICutFillCalculator abstraction; the production
        // implementation isolates the platform limitation (no reliable read-only volume path)
        // behind that contract. Tool discovered by assembly scanning.
        services.AddSingleton<ICutFillCalculator, Civil3DCutFillCalculator>();
        services.AddSingleton<IWorkflowHandler<CutFillWorkflow, CutFillReport>,
            CutFillWorkflowHandler>();

        // Corridor analysis workflow (Phase 7H): the read-only corridor_analysis_report
        // workflow. The tool resolves the handler through IWorkflowDispatcher; only the handler
        // is registered here.
        services.AddSingleton<IWorkflowHandler<CorridorAnalysisWorkflow, CorridorAnalysisReport>,
            CorridorAnalysisWorkflowHandler>();

        // LandXML export workflow (Phase 7I): the export_landxml workflow. The workflow depends
        // only on the ILandXmlExporter abstraction; the production exporter honestly reports a
        // structured not-supported result (a live interactive document context is required for
        // LandXML export, which the read-only workflow layer does not perform). Only the handler
        // and the abstraction implementation are registered here.
        services.AddSingleton<ILandXmlExporter, Civil3DLandXmlExporter>();
        services.AddSingleton<IWorkflowHandler<LandXmlExportWorkflow, LandXmlExportReport>,
            LandXmlExportWorkflowHandler>();

        // Bridge services.
        services.AddSingleton<IEndpointInfoProvider, BridgeInfoProvider>();
        services.AddSingleton<IApplicationContext, AutodeskApplicationContext>();
        services.AddSingleton<ToolDispatcher>();
        services.AddSingleton<IToolExecutor>(sp => sp.GetRequiredService<ToolDispatcher>());

        // Protocol handlers + router.
        services.AddSingleton<IProtocolHandler, HandshakeHandler>();
        services.AddSingleton<IProtocolHandler, ListToolsHandler>();
        services.AddSingleton<IProtocolHandler, ExecuteToolHandler>();
        services.AddSingleton<IProtocolHandler, PingHandler>();
        services.AddSingleton<IProtocolHandler, ShutdownHandler>();
        services.AddSingleton<JsonRpcRouter>();
        services.AddSingleton<IRpcRouter>(sp => sp.GetRequiredService<JsonRpcRouter>());

        // Registration (endpoint descriptor).
        services.AddSingleton(new EndpointRegistryOptions
        {
            DirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutodeskMcp",
                "endpoints"),
        });
        services.AddSingleton<IEndpointRegistrar, EndpointRegistrar>();

        // Communication + host.
        services.AddSingleton(sp =>
        {
            BridgeHostOptions hostOptions = sp.GetRequiredService<BridgeHostOptions>();
            return new NamedPipeServerHost(
                hostOptions.PipeName,
                hostOptions.MaxConcurrentConnections,
                sp.GetRequiredService<IRpcRouter>(),
                sp.GetRequiredService<ILogger<NamedPipeServerHost>>());
        });
        services.AddSingleton<BridgeHost>();

        return services;
    }

    private static VersionInformation ParseVersion(string value, VersionInformation fallback)
        => VersionInformation.TryParse(value, out VersionInformation version) ? version : fallback;
}
