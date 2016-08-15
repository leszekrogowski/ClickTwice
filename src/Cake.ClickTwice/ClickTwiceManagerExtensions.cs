using System.Collections.Generic;
using Cake.Common.Tools.MSBuild;
using System.Linq;
using ClickTwice.Publisher.Core.Handlers;
using ClickTwice.Publisher.Core.Loggers;

namespace Cake.ClickTwice
{
    public static class ClickTwiceManagerExtensions
    {
        public static ClickTwiceManager SetBuildPlatform(this ClickTwiceManager manager, MSBuildPlatform platform)
        {
            manager.Platform = platform == MSBuildPlatform.Automatic ? "AnyCPU" : platform.ToString();
            return manager;
        }

        public static ClickTwiceManager SetConfiguration(this ClickTwiceManager manager, string configuration)
        {
            manager.Configuration = configuration;
            return manager;
        }

        public static ClickTwiceManager WithHandler(this ClickTwiceManager manager, IHandler handler)
        {
            var output = handler as IOutputHandler;
            var input = handler as IInputHandler;
            if (output != null)
            {
                manager.OutputHandlers.Add(output);
            }
            if (input != null)
            {
                manager.InputHandlers.Add(input);
            }
            return manager;
        }

        public static ClickTwiceManager WithHandlers(this ClickTwiceManager manager, IEnumerable<IHandler> handlers)
        {
            foreach (var handler in handlers)
            {
                manager.WithHandler(handler);
            }
            return manager;
        }

        public static ClickTwiceManager LogTo(this ClickTwiceManager manager, IPublishLogger logger)
        {
            manager.Loggers.Add(logger);
            return manager;
        }

        public static ClickTwiceManager CleanAfterBuild(this ClickTwiceManager manager)
        {
            manager.CleanOutput = true;
            return manager;
        }

        public static ClickTwiceManager ThrowOnHandlerFailure(this ClickTwiceManager manager)
        {
            manager.ErrorAction = resp =>
            {
                throw new PublishException(resp.Where(r => r.Result == HandlerResult.Error));
            };
            return manager;
        }

        public static ClickTwiceManager ForceRebuild(this ClickTwiceManager manager)
        {
            manager.ForceBuild = true;
            return manager;
        }

        public static ClickTwiceManager WithVersion(this ClickTwiceManager manager, string s)
        {
            manager.PublishVersion = s;
            return manager;
        }

        //public static ClickTwiceManager UseBuildAction(this ClickTwiceManager manager,
        //    Action<CakePublishManager> buildAction)
        //{
        //    manager.BuildAction = buildAction;
        //    return manager;
        //}
    }
}