﻿using Autofac;
using Autofac.Integration.WebApi;
using Owin;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Web.Http;
using WeText.Common.Messaging;
using WeText.Common.Repositories;
using WeText.DomainRepositories;
using WeText.Messaging.RabbitMq;
using WeText.Common.Services;
using Microsoft.Owin.Hosting;
using System;

namespace WeText.Service
{
    internal sealed class WeTextService : Common.Services.Service
    {
        const string SearchPath = "services";

        static void LoadServices(ContainerBuilder builder)
        {
            var searchFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), SearchPath);
            foreach (var file in Directory.EnumerateFiles(searchFolder, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    var exportedTypes = assembly.GetExportedTypes();
                    if (exportedTypes.Any(t => typeof(IService).IsAssignableFrom(t)))
                    {

                    }

                    if (exportedTypes.Any(t => t.IsSubclassOf(typeof(ApiController))))
                    {
                        builder.RegisterApiControllers(assembly).InstancePerRequest();
                    }
                }
                catch { }
            }
        }

        public void Configuration(IAppBuilder app)
        {
            var builder = new ContainerBuilder();
            builder.Register(x => new RabbitMqCommandBus("localhost", "wetext_command_exchange")).As<ICommandSender>();
            builder.Register(x => new RabbitMqEventBus("localhost", "wetext_event_exchange")).As<IEventPublisher>();
            builder.Register(x => new MongoDomainRepository(x.Resolve<IEventPublisher>())).As<IDomainRepository>();

            // Loads the microservices.
            LoadServices(builder);

            var container = builder.Build();
            app.UseAutofacMiddleware(container);

            HttpConfiguration config = new HttpConfiguration();

            config.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            app.UseAutofacWebApi(config);
            app.UseWebApi(config);
        }

        public override void Start(object[] args)
        {
            var url = "http://+:9023/";
            using (WebApp.Start<WeTextService>(url: url))
            {
                Console.WriteLine("Service started.");
                Console.ReadLine();
            }
        }
    }
}
