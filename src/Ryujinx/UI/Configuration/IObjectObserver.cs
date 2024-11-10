using System;

namespace Ryujinx.UI.Configuration
{

    interface IObjectObserver
    {

        bool HasChanged { get; }

        void Reset();

        void Destroy();

    }

}
