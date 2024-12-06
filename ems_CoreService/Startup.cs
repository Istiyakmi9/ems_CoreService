using bt_lib_common_services.Configserver;
using bt_lib_common_services.KafkaService.code;
using bt_lib_common_services.KafkaService.interfaces;
using bt_lib_common_services.Model;
using ems_CoreService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceLayer.Interface;

namespace OnlineDataBuilder
{
    public class Startup
    {
        private IWebHostEnvironment _env { set; get; }
        private IConfiguration _configuration { get; }
        private readonly RegisterServices _registerService;
        private static string CorsPolicy = "BottomhalfCORS";

        public Startup(IWebHostEnvironment env)
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(env.ContentRootPath)
                    .AddJsonFile($"appsettings.json", false, false)
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", false, false)
                    .AddJsonFile("staffingbill.json", false, false)
                    .AddEnvironmentVariables();

                _configuration = config.Build();
                _env = env;
                _registerService = new RegisterServices(env, _configuration, CorsPolicy);
            }
            catch
            {
                throw;
            }
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddHttpContextAccessor();

            // register service layer classes
            _registerService.RegisterServiceLayerServices(services);

            // register folder paths
            _registerService.RegisterFolderPaths(_configuration, _env, services);

            // register database
            _registerService.RegisterDatabase(services, _configuration);

            // register common utility for web config services
            _registerService.RegisterCommonUtility(services);

            // Subscribe the kafka service
            services.AddSingleton<IKafkaConsumerService>(x =>
                new KafkaConsumerService(
                    KafkaTopicNames.DAILY_JOBS_MANAGER,
                    FetchGithubConfigurationService.getInstance(GitRepositories.EMS_CONFIG_SERVICE).Result
                )
            );
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime, IAutoTriggerService autoTriggerService)
        {
            ConfigureMiddlewares.ConfigureDevelopmentMode(app, _env);

            ConfigureMiddlewares.OnApplicationStartUp(lifetime, autoTriggerService);

            ConfigureMiddlewares.Configure(app, CorsPolicy);
        }


    }
}