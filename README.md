# Namespaced Items

This is a project that brings essentially minecraft's item naming system to Muck. The aim is to remove item IDs, and make a persistent unique string ID instead. This will be more stable when serialized to save data, and even if order of operations may not be the same on all clients, their IDs will not be out of sync.

In addition to this, it also allows items to have instance data, and basically revamps the item system to be modular based on interfaces, instead of a single class.

# Building

1) Build the preloader patcher.
2) In ``BepInEx\config\BepInEx.cfg`` make sure ``[Preloader] DumpAssemblies = true``.
    - If you're using .NET 4.x libraries, you may need to create a directory junction between ``libs\Facades\`` and ``Muck_Data\Managed\``.
    - Essentially, it will look for unity libraries (to reference in the dumped assembly) in the same directory as ``mscorlib``. For some reason, it will first search in ``Facades``, so that can be used as a workaround to the issue.
3) Run the game and let the preloader finish.
    - It's probably gonna throw errors at runtime at this stage, just as long as the preloader works it's all fine.
4) Drag ``BepInEx\DumpedAssemblies\Assembly-CSharp.dll`` onto assembly publicizer, so you can reference publicized modified assembly in the plugin.
5) Restart OmniSharp or whatever, because it probably won't detect the changes otherwise.
6) Build the plugin project.