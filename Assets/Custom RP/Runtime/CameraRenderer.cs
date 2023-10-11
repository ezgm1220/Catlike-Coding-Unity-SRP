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

    CullingResults cullingResults;// �޳����

    Lighting lighting = new Lighting();// Lighting ʵ��

    public void Render(ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing,ShadowSettings shadowSettings)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();// ��ÿ�������ʹ�ò�ͬ��Sample Name
        PrepareForSceneWindow();// ����Scene�����µ�UI
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
        //�����������˳�������������ǻ���������������
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };

        //���������֧�ֵ�Shader Pass�ͻ���˳��ȵ�����
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            // ���ö�Ӧ����Ⱦ��ʽ
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps // ���ù�����ͼ
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);

        //����������ЩVisible Objects�����ã�����֧�ֵ�RenderQueue��
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        //��ȾCullingResults�ڲ�͸����VisibleObjects
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        
        //��ӡ�������պС�ָ�DrawSkyboxΪScriptableRenderContext�����к����������������Ϊʲô˵Unity�Ѿ������Ƿ�װ���˺ܶ�����Ҫ�õ��ĺ�����SPR�Ļ���~
        context.DrawSkybox(camera);
        
        //��Ⱦ͸������
        //���û���˳��Ϊ�Ӻ���ǰ
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
       
        //ע��ֵ����
        drawingSettings.sortingSettings = sortingSettings;
        
        //���˳�RenderQueue����Transparent������
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        
        //����͸������
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    bool Cull(float maxShadowDistance)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            // ��Ⱦ����ȡ��С�ľ���
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