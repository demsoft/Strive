using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FluentValidation;
using FluentValidation.AspNetCore;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Newtonsoft.Json;
using StackExchange.Redis.Extensions.Core.Abstractions;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Newtonsoft;
using Strive.Auth;
using Strive.Config;
using Strive.Core;
using Strive.Core.Domain.Entities;
using Strive.Core.Services.Media;
using Strive.Extensions;
using Strive.Hubs.Core;
using Strive.Hubs.Equipment;
using Strive.Infrastructure;
using Strive.Infrastructure.Data;
using Strive.Infrastructure.KeyValue;
using Strive.Infrastructure.KeyValue.Abstractions;
using Strive.Infrastructure.KeyValue.InMemory;
using Strive.Infrastructure.KeyValue.Redis;
using Strive.Infrastructure.Scheduler;
using Strive.Infrastructure.Serialization;
using Strive.Infrastructure.Sfu;
using Strive.Messaging.Consumers;
using Strive.Messaging.SFU.SendContracts;
using Strive.Services;
using Strive.Utilities;

namespace Strive
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        static Startup()
        {
            BsonSerializer.RegisterSerializer(new JTokenBsonSerializer());
            BsonSerializer.RegisterSerializer(new EnumSerializer<PermissionType>(BsonType.String));
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();

            // Authentication
            var authOptions = Configuration.GetRequired<AuthOptions>("Authentication");
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(
                JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = authOptions.Authority;
                    options.TokenValidationParameters =
                        new TokenValidationParameters {ValidateAudience = false, ValidIssuer = authOptions.Issuer};

                    options.RequireHttpsMetadata = !authOptions.NoSslRequired;

                    options.AcceptTokenFromQuery();
                });
            services.AddSingleton<IAuthorizationHandler, UserIsModeratorOfConferenceHandler>();

            var sfuOptions = Configuration.GetRequired<SfuOptions>("SFU");
            var signingKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(sfuOptions.TokenSecret ??
                                                                              throw new ArgumentException(
                                                                                  "SFU token secret not set")));

            services.AddSingleton<IOptions<SfuJwtOptions>>(new OptionsWrapper<SfuJwtOptions>(new SfuJwtOptions
            {
                Audience = sfuOptions.TokenAudience,
                Issuer = sfuOptions.TokenIssuer,
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
                ValidFor = sfuOptions.TokenExpiration,
            }));

            services.AddSingleton<IOptions<SfuConnectionOptions>>(new OptionsWrapper<SfuConnectionOptions>(
                new SfuConnectionOptions(sfuOptions.UrlTemplate ??
                                         throw new ArgumentException("SFU url template not set."))));

            // TURN (optional): relays media for participants that cannot reach the SFU directly
            services.Configure<TurnOptions>(Configuration.GetSection("Turn"));
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<ITurnCredentialFactory, TurnCredentialFactory>();

            // SignalR
            // Since .NET 7, hub and API parameters are bound from DI if the container can resolve their type. Autofac
            // reports collection types (e.g. IReadOnlyList<T>) as resolvable, so client arguments would be injected
            // instead. All injected parameters use [FromServices] explicitly.
            services.AddSignalR(options => options.DisableImplicitFromServicesParameters = true)
                .AddNewtonsoftJsonProtocol(options => { JsonConfig.Apply(options.PayloadSerializerSettings); });

            services.AddMvc().ConfigureApiBehaviorOptions(options =>
                {
                    options.UseInvalidModelStateToError();
                    options.DisableImplicitFromServicesParameters = true;
                })
                .AddNewtonsoftJson(options => { JsonConfig.Apply(options.SerializerSettings); });

            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<Startup>();
            services.AddValidatorsFromAssemblyContaining<CoreModule>();

            var healthChecks = services.AddHealthChecks();

            // KeyValueDatabase
            var keyValueOptions = Configuration.GetRequired<KeyValueDatabaseConfig>("KeyValueDatabase");
            if (keyValueOptions.UseInMemory)
            {
                services.AddSingleton<IKeyValueDatabase, InMemoryKeyValueDatabase>(services =>
                    new InMemoryKeyValueDatabase(new InMemoryKeyValueData(),
                        services.GetRequiredService<IOptions<KeyValueDatabaseOptions>>()));
            }
            else
            {
                var config = keyValueOptions.Redis ?? new RedisConfiguration();
                services.AddStackExchangeRedisExtensions<NewtonsoftSerializer>(config);
                services.AddSingleton(s => s.GetRequiredService<IRedisDatabase>().Database);
                services.AddSingleton<IKeyValueDatabase, RedisKeyValueDatabase>();

                // reuse the application's connection, the configuration may use Hosts instead of a connection string
                healthChecks.AddRedis(s => s.GetRequiredService<IRedisDatabase>().Database.Multiplexer);
            }

            // MongoDb
            services.Configure<MongoDbOptions>(Configuration.GetSection("MongoDb"));
            services.AddHostedService<MongoDbBuilder>();

            var mongoOptions = Configuration.GetRequired<MongoDbOptions>("MongoDb");
            var healthCheckMongoClient = new MongoClient(mongoOptions.ConnectionString);
            healthChecks.AddMongoDb(_ => healthCheckMongoClient);

            services.Configure<HealthCheckPublisherOptions>(options =>
            {
                options.Predicate = check => check.Tags.Contains("ready");
            });

            // Masstransit / RabbitMQ
            services.Configure<SfuOptions>(Configuration.GetSection("SFU"));
            services.Configure<RabbitMqOptions>(Configuration.GetSection("RabbitMq"));

            var rabbitMqOptions = Configuration.GetRequired<RabbitMqOptions>("RabbitMq");

            services.AddMassTransit(config =>
            {
                //x.AddSignalRHub<CoreHub>();
                config.AddConsumersFromNamespaceContaining<ParticipantKickedConsumer>();
                config.AddConsumer<MediatrNotificationConsumer>();

                if (rabbitMqOptions.UseInMemory)
                {
                    Uri schedulerEndpoint = new("queue:scheduler");
                    config.AddMessageScheduler(schedulerEndpoint);
                    config.UsingInMemory((context, configurator) =>
                    {
                        // MassTransit 8 defaults to System.Text.Json, keep Newtonsoft as in MassTransit 7
                        configurator.UseNewtonsoftJsonSerializer();
                        configurator.UseNewtonsoftJsonDeserializer();

                        configurator.UseInMemoryScheduler("scheduler");
                        configurator.ConfigureEndpoints(context);

                        ScheduledMediator.Configure(configurator, context);
                    });
                }
                else
                {
                    config.AddDelayedMessageScheduler();
                    config.UsingRabbitMq((context, configurator) =>
                    {
                        if (rabbitMqOptions.RabbitMq != null)
                            configurator.ConfigureOptions(rabbitMqOptions.RabbitMq);

                        configurator.UseDelayedMessageScheduler();

                        configurator.ConfigureEndpoints(context);

                        ScheduledMediator.Configure(configurator, context);

                        configurator.ConfigurePublishMessage<MediaStateChanged>(sfuOptions);
                        configurator.ConfigurePublishMessage<ChangeParticipantProducer>(sfuOptions);
                        configurator.ConfigurePublishMessage<ParticipantLeft>(sfuOptions);

                        configurator.ReceiveEndpoint(sfuOptions.ReceiveQueue, e =>
                        {
                            e.Durable = false;

                            e.ConfigureConsumer<StreamsUpdatedConsumer>(context);
                            e.ConfigureConsumer<NotifyConnectionConsumer>(context);
                        });

                        // the SFU parses these messages, so keep the Newtonsoft wire format from MassTransit 7
                        configurator.UseNewtonsoftJsonSerializer();
                        configurator.UseNewtonsoftJsonDeserializer();
                        configurator.ConfigureNewtonsoftJsonSerializer(jsonConfig =>
                        {
                            jsonConfig.DefaultValueHandling = DefaultValueHandling.Include;
                            JsonConfig.Apply(jsonConfig);
                            return jsonConfig;
                        });
                    });
                }
            });
            services.AddMediator();

            // Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo {Title = "Strive API", Version = "v1"});

                var scheme = new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please insert JWT with Bearer into field",
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                };

                // Swagger 2.+ support
                c.AddSecurityDefinition("Bearer", scheme);
                c.AddSecurityRequirement(new OpenApiSecurityRequirement {{scheme, new List<string>()}});
            });

            services.AddMediatR(config =>
                config.RegisterServicesFromAssemblies(typeof(Startup).Assembly, typeof(CoreModule).Assembly));

            if (Environment.IsDevelopment())
                services.AddCors(options =>
                {
                    options.AddPolicy("AllowAll",
                        builder =>
                        {
                            builder.WithOrigins("http://localhost:55103").AllowAnyMethod().AllowAnyHeader()
                                .AllowCredentials();
                        });
                });

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            // Now register our services with Autofac container.
            var builder = new ContainerBuilder();

            builder.RegisterModule(new CoreModule());
            builder.RegisterModule(new InfrastructureModule());
            builder.RegisterModule(new PresentationModule());

            if (Environment.IsDevelopment())
                builder.RegisterGeneric(typeof(LoggingBehavior<,>)).As(typeof(IPipelineBehavior<,>));

            builder.Populate(services);
            var container = builder.Build();
            // Create the IServiceProvider based on the container.
            return new AutofacServiceProvider(container);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseHsts();

            if (env.IsDevelopment()) app.UseCors("AllowAll");

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), 
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Strive API V1"));

            app.UseAuthentication();

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller}/{action=Index}/{id?}");
                endpoints.MapHub<CoreHub>("/signalr");
                endpoints.MapHub<EquipmentHub>("/equipment-signalr");
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health/ready",
                    new HealthCheckOptions {Predicate = check => check.Tags.Contains("ready")});

                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions());
            });
        }
    }
}