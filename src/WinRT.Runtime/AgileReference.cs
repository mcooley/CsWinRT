﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace WinRT
{
#if EMBED
    internal
#else
    public
#endif 
    class AgileReference : IDisposable
    {
        private readonly static Guid CLSID_StdGlobalInterfaceTable = new(0x00000323, 0, 0, 0xc0, 0, 0, 0, 0, 0, 0, 0x46);
        private readonly static Lazy<IGlobalInterfaceTable> Git = new Lazy<IGlobalInterfaceTable>(() => GetGitTable());
        private readonly IAgileReference _agileReference;
        private readonly IntPtr _cookie;
        private bool disposed;

        public unsafe AgileReference(IObjectReference instance) 
        {
            if(instance?.ThisPtr == null)
            {
                return;
            }   

            IntPtr agileReference = default;
            Guid iid = IUnknownVftbl.IID;
            try
            {
                Marshal.ThrowExceptionForHR(Platform.RoGetAgileReference(
                    0 /*AGILEREFERENCE_DEFAULT*/,
                    ref iid,
                    instance.ThisPtr,
                    &agileReference));
#if NET
                _agileReference = (IAgileReference)new SingleInterfaceOptimizedObject(typeof(IAgileReference), ObjectReference<ABI.WinRT.Interop.IAgileReference.Vftbl>.Attach(ref agileReference));
#else
                _agileReference = ABI.WinRT.Interop.IAgileReference.FromAbi(agileReference).AsType<ABI.WinRT.Interop.IAgileReference>();
#endif
            }
            catch(TypeLoadException)
            {
                _cookie = Git.Value.RegisterInterfaceInGlobal(instance, iid);
            }
            finally
            {
                MarshalInterface<IAgileReference>.DisposeAbi(agileReference);
            }
        }
        public IObjectReference Get() => _cookie == IntPtr.Zero ? _agileReference?.Resolve(IUnknownVftbl.IID) : Git.Value?.GetInterfaceFromGlobal(_cookie, IUnknownVftbl.IID);

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (_cookie != IntPtr.Zero)
                {
                    try
                    {
                        Git.Value.RevokeInterfaceFromGlobal(_cookie);
                    }
                    catch(ArgumentException)
                    {
                        // Revoking cookie from GIT table may fail if apartment is gone.
                    }
                }
                disposed = true;
            }
        }

        private static unsafe IGlobalInterfaceTable GetGitTable()
        {
            Guid gitClsid = CLSID_StdGlobalInterfaceTable;
            Guid gitIid = ABI.WinRT.Interop.IGlobalInterfaceTable.IID;
            IntPtr gitPtr = default;

            try
            {
                Marshal.ThrowExceptionForHR(Platform.CoCreateInstance(
                    ref gitClsid,
                    IntPtr.Zero,
                    1 /*CLSCTX_INPROC_SERVER*/,
                    ref gitIid,
                    &gitPtr));
                return ABI.WinRT.Interop.IGlobalInterfaceTable.FromAbi(gitPtr).AsType<ABI.WinRT.Interop.IGlobalInterfaceTable>();
            }
            finally
            {
                MarshalInterface<IGlobalInterfaceTable>.DisposeAbi(gitPtr);
            }
        }

        ~AgileReference()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

#if EMBED
    internal
#else
    public 
#endif
    sealed class AgileReference<T> : AgileReference
        where T : class
    {
        public unsafe AgileReference(IObjectReference instance)
            : base(instance)
        {
        }

        public new T Get() 
        {
            using var objRef = base.Get();
            return ComWrappersSupport.CreateRcwForComObject<T>(objRef?.ThisPtr ?? IntPtr.Zero);
        }
    }
}