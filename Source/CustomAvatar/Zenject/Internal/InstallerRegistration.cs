﻿using ModestTree;
using System;
using System.Reflection;
using Zenject;

namespace CustomAvatar.Zenject.Internal
{
    internal class InstallerRegistration
    {
        private static readonly MethodInfo _installMethod = typeof(DiContainer).GetMethod("Install", BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.Standard, new[] { typeof(object[]) }, null);

        public readonly Type installer;

        private object[] _extraArgs;
        private InstallerRegistrationOnTarget _target;

        public InstallerRegistration(Type installer)
        {
            Assert.DerivesFrom(installer, typeof(Installer));

            this.installer = installer;

            _extraArgs = new object[0];
        }

        public InstallerRegistration WithArguments(params object[] extraArgs)
        {
            _extraArgs = extraArgs;
            return this;
        }

        public InstallerRegistrationOnContext OnContext(string sceneName, string contextName)
        {
            var target = new InstallerRegistrationOnContext(sceneName, contextName);

            _target = target;

            return target;
        }

        public InstallerRegistrationOnMonoInstaller<T> OnMonoInstaller<T>() where T : MonoInstaller
        {
            var target = new InstallerRegistrationOnMonoInstaller<T>();

            _target = target;

            return target;
        }

        internal bool TryInstallInto(Context context)
        {
            if (!_target.ShouldInstall(context)) return false;

            _installMethod.MakeGenericMethod(installer).Invoke(context.Container, new[] { _extraArgs });

            return true;
        }
    }
}
