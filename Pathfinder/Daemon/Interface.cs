﻿using System.Xml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Pathfinder.Daemon
{
    public interface IInterface
    {
        string InitialServiceName { get; }

        void OnCreate(Instance instance);
        void Draw(Instance instance, Rectangle bounds, SpriteBatch sb);
        void LoadInstance(Instance instance, XmlReader reader);
        void InitFiles(Instance instance);
        void LoadInit(Instance instance);
        void OnNavigatedTo(Instance instance);
        void OnUserAdded(Instance instance, string name, string pass, byte type);
    }

    public class Interface : IInterface
    {
        public virtual string InitialServiceName
        {
            get
            {
                return this.GetType().FullName;
            }
        }

        public virtual void OnCreate(Instance instance) {}
        public virtual void Draw(Instance instance, Rectangle bounds, SpriteBatch sb) {}
        public virtual void LoadInstance(Instance instance, XmlReader reader) {}
        public virtual void InitFiles(Instance instance) {}
        public virtual void LoadInit(Instance instance) {}
        public virtual void OnNavigatedTo(Instance instance) {}
        public virtual void OnUserAdded(Instance instance, string name, string pass, byte type) {}
    }
}