﻿using System.Threading.Tasks;

namespace Dropcraft.Common
{
    public interface IRuntimeEngine
    {
        IRuntimeContext RuntimeContext { get; }

        Task Start();
        void Stop();
    }
}