using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{

    const string bufferName = "Lighting";

    //static int // ����������ɫ�����Եı�ʶ��
    //    dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
    //    dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
    const int maxDirLightCount = 4;// �ƹ��������

    // �����µ����Ա�ʶ��
    static int
        //dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
        //dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");

    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount];


    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CullingResults cullingResults;

    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

        int dirLightCount = 0;

        for (int i = 0; i < visibleLights.Length; i++)// ������Դ
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)// ����ԴΪ�����ʱ
            {
                SetupDirectionalLight(dirLightCount++, ref visibleLight);// ���ݹ�Դ����
                if (dirLightCount >= maxDirLightCount)// ʹ������������������������
                {
                    break;
                }
            }
        }

        buffer.SetGlobalInt(dirLightCountId, dirLightCount);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
    }

    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults
    )
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName);
        SetupLights();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        dirLightColors[index] = visibleLight.finalColor;
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
    }
}