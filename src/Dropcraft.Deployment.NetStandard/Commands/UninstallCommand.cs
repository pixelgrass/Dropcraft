﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;

namespace Dropcraft.Deployment.Commands
{
    public class UninstallCommand : DeploymentCommand
    {
        private CommandArgument _package;
        private CommandOption _productPath;
        private CommandOption _removeDependencies;

        public UninstallCommand()
        {
            Name = "uninstall";
        }

        protected override void Define(CommandLineApplication cmdApp, Action<string> logErrorAction)
        {
            cmdApp.Description = "Uninstalls provided packages from the product";
            cmdApp.HelpOption(CommandHelper.HelpOption);

            _package = cmdApp.Argument("[package]", "Package to install", true);
            _productPath = cmdApp.Option("--path <installationPath>", "Product installation path", CommandOptionType.SingleValue);

            _removeDependencies = cmdApp.Option("-r|--remove-deps", "Remove any dependent packages if they are not referenced elsewhere ", CommandOptionType.NoValue);
        }

        protected override async Task<int> Execute(CommandLineApplication app, Action<string> logErrorAction)
        {
            if (_package.Values.Count == 0)
            {
                logErrorAction($"Missed packages list");
                return 1;
            }

            if (MissedOption(_productPath, logErrorAction))
                return 1;

            return await Task.FromResult(0);
        }
    }
}