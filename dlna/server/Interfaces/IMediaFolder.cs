﻿using System.Collections.Generic;

namespace NMaier.SimpleDlna.Server
{
  public interface IMediaFolder : IMediaItem
  {
    int ChildCount { get; }

    IEnumerable<IMediaFolder> ChildFolders { get; }

    IEnumerable<IMediaResource> ChildItems { get; }

    IMediaFolder Parent { get; set; }

    void AddResource(IMediaResource res);

    void Cleanup();

    void RemoveResource(IMediaResource res);

    void Sort(IComparer<IMediaItem> comparer, bool descending);
  }
}
