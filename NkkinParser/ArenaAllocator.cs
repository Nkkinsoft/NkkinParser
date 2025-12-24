using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NkkinParser;

/// <summary>
/// A high-performance arena allocator that manages memory slabs.
/// Specifically designed for NativeAOT and low-allocation scenarios.
/// </summary>
public sealed unsafe class ArenaAllocator : IDisposable
{
    private const int DefaultSlabSize = 1024 * 64; // 64KB
    private readonly List<IntPtr> _slabs = new();
    private byte* _currentSlab;
    private int _offset;
    private int _slabSize;

    public ArenaAllocator(int slabSize = DefaultSlabSize)
    {
        _slabSize = slabSize;
        AllocateNewSlab();
        _currentManagedSlab = new object[4096];
        _managedSlabs.Add(_currentManagedSlab);
        _managedOffset = 0;
    }

    private void AllocateNewSlab()
    {
        IntPtr ptr = Marshal.AllocHGlobal(_slabSize);
        _currentSlab = (byte*)ptr;
        _slabs.Add(ptr);
        _offset = 0;
        
        // Zero out memory if needed, but for our nodes we usually initialize all fields
        Unsafe.InitBlock(_currentSlab, 0, (uint)_slabSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* Allocate<T>() where T : unmanaged
    {
        int size = sizeof(T);
        // Align to 8 bytes for performance
        int alignedSize = (size + 7) & ~7;

        if (_offset + alignedSize > _slabSize)
        {
            if (alignedSize > _slabSize)
            {
                // For very large objects, allocate a custom slab
                IntPtr largePtr = Marshal.AllocHGlobal(alignedSize);
                _slabs.Add(largePtr);
                Unsafe.InitBlock((void*)largePtr, 0, (uint)alignedSize);
                return (T*)largePtr;
            }
            AllocateNewSlab();
        }

        T* result = (T*)(_currentSlab + _offset);
        _offset += alignedSize;
        return result;
    }

    // Since we want to use classes for DOM nodes (for ease of use and inheritance), 
    // but the user suggested "arena/pooled allocation", we can blend them.
    // However, allocating managed objects in an arena is tricky without custom GC.
    // Given the "unsafe" and "low-level" requirements, switching DOM nodes to structs
    // or using Unsafe.AsPointer on managed objects (risky) is an option.
    // Better: Use a pool of managed objects or a custom "ManagedArena" like the previous slab one,
    // but optimized.

    private readonly List<object[]> _managedSlabs = new();
    private int _managedOffset;
    private object[] _currentManagedSlab;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T AllocateManaged<T>() where T : class, new()
    {
        if (_currentManagedSlab == null || _managedOffset >= _currentManagedSlab.Length)
        {
            _currentManagedSlab = new object[4096];
            _managedSlabs.Add(_currentManagedSlab);
            _managedOffset = 0;
        }

        T obj = new T();
        _currentManagedSlab[_managedOffset++] = obj;
        return obj;
    }

    public void Dispose()
    {
        foreach (var ptr in _slabs)
        {
            Marshal.FreeHGlobal(ptr);
        }
        _slabs.Clear();
        _managedSlabs.Clear();
    }
}