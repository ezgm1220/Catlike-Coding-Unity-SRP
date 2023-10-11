using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{

    const string bufferName = "Render Camera";

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    Camera camera;

    CullingResults cullingResults;// 剔除结果

    Lighting lighting = new Lighting();// Lighting 实例

    public void Render(ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing,ShadowSettings shadowSettings)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();// 对每个摄像机使用不同的Sample Name
        PrepareForSceneWindow();// 绘制Scene窗口下的UI
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }

        //Setup();
        //lighting.Setup(context, cullingResults, shadowSettings);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context, cullingResults, shadowSettings);
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
        DrawUnsupportedShaders();
        DrawGizmos();
        lighting.Cleanup();
        Submit();

    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
    {
        //决定物体绘制顺序是正交排序还是基于深度排序的配置
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };

        //决定摄像机支持的Shader Pass和绘制顺序等的配置
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            // 设置对应的渲染方式
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps // 设置光照贴图
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);

        //决定过滤哪些Visible Objects的配置，包括支持的RenderQueue等
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        //渲染CullingResults内不透明的VisibleObjects
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        
        //添加“绘制天空盒”指令，DrawSkybox为ScriptableRenderContext下已有函数，这里就体现了为什么说Unity已经帮我们封装好了很多我们要用到的函数，SPR的画笔~
        context.DrawSkybox(camera);
        
        //渲染透明物体
        //设置绘制顺序为从后往前
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
       
        //注意值类型
        drawingSettings.sortingSettings = sortingSettings;
        
        //过滤出RenderQueue属于Transparent的物体
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        
        //绘制透明物体
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    bool Cull(float maxShadowDistance)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            // 渲染距离取最小的距离
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags <= CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
                camera.backgroundColor.linear : Color.clear
        );
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }


}