﻿using System;
using ProjBobcat.Class.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    public interface IResourceInfoResolver
    {
        string BasePath { get; set; }
        VersionInfo VersionInfo { get; set; }
        Task<IEnumerable<IGameResource>> ResolveResourceTaskAsync();
        IEnumerable<IGameResource> ResolveResource();

        event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveEvent;
    }
}