﻿using System;
using System.Threading.Tasks;
using Dropcraft.Common;
using Microsoft.Extensions.CommandLineUtils;

namespace Dropcraft.Deployment.Commands
{
    public abstract class DeploymentCommand
    {
        public string Name { get; set; }

        public void Register(CommandLineApplication app, Action<string> logErrorAction)
        {
            app.Command(Name, cmdApp =>
            {
                Define(cmdApp, logErrorAction);
                cmdApp.OnExecute(async () => await Execute(cmdApp, logErrorAction));

            });
        }

        protected abstract void Define(CommandLineApplication app, Action<string> logErrorAction);

        protected abstract Task<int> Execute(CommandLineApplication app, Action<string> logErrorAction);

        protected virtual DeploymentConfiguration GetConfiguration(CommandLineApplication app, string productPath, string packagesPath, string framework)
        {
            return new DeploymentConfiguration(productPath, packagesPath, framework);
        }

        protected virtual IDeploymentEngine GetDeploymentEngine(CommandLineApplication app, DeploymentConfiguration configuration)
        {
            return configuration.CreatEngine();
        }

        protected bool MissedOption(CommandOption option, Action<string> logErrorAction)
        {
            if (option.HasValue())
                return false;

            logErrorAction($"Missed option: {option.ValueName}");
            return true;
        }

    }
}
