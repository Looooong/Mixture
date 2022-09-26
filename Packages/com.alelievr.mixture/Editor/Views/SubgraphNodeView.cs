using GraphProcessor;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mixture
{
  [NodeCustomEditor(typeof(SubgraphNode))]
  public class SubgraphNodeView : MixtureNodeView
  {
    SubgraphNode _node;

    DropdownField _previewOutputField;

    public override void Enable(bool fromInspector)
    {
      base.Enable(fromInspector);

      _node = nodeTarget as SubgraphNode;

      _previewOutputField = new DropdownField(
        "Preview Output",
        _node.subgraph?.outputNode?.outputTextureSettings?.Select(setting => setting.name)?.ToList(),
        _node.previewOutputIndex
      )
      { visible = _node.previewOutputIndex > -1 };
      _previewOutputField.RegisterValueChangedCallback(e =>
      {
        _node.previewOutputIndex = _previewOutputField.index;
        NotifyNodeChanged();
      });

      var subgraphTextureField = new ObjectField("Subgraph Texture")
      {
        value = _node.subgraphTexture,
        objectType = typeof(CustomRenderTexture)
      };
      subgraphTextureField.RegisterValueChangedCallback(e =>
      {
        _node.subgraphTexture = e.newValue as CustomRenderTexture;
        UpdateSubgraph();
        title = _node.name;
      });

      controlsContainer.Add(_previewOutputField);
      controlsContainer.Add(subgraphTextureField);

      _node.onAfterEdgeDisconnected += UpdateSubgraph;

      UpdateSubgraph();
    }

    public override void Disable()
    {
      base.Disable();

      if (_node != null) _node.onAfterEdgeDisconnected -= UpdateSubgraph;
    }

    void UpdateSubgraph(SerializableEdge _) => UpdateSubgraph();

    void UpdateSubgraph()
    {
      if (_node.subgraph != null)
      {
        _node.subgraph.onExposedParameterModified -= UpdateSubgraphInputs;
        _node.subgraph.onExposedParameterListChanged -= UpdateSubgraphInputs;
        _node.subgraph.outputNode.onPortsUpdated -= UpdateSubgraphOutputs;
      }

      _node.subgraph = MixtureDatabase.GetGraphFromTexture(_node.subgraphTexture);

      if (ValidateSubgraph())
      {
        _node.subgraph.onExposedParameterModified += UpdateSubgraphInputs;
        _node.subgraph.onExposedParameterListChanged += UpdateSubgraphInputs;
        _node.subgraph.outputNode.onPortsUpdated += UpdateSubgraphOutputs;

        _node.UpdateOutputTextures();
        UpdatePreviewUI();
      }
      else
      {
        _node.ReleaseOutputTextures();
        _node.previewOutputIndex = -1;
      }

      _previewOutputField.visible = _node.previewOutputIndex > -1;

      ForceUpdatePorts();
      NotifyNodeChanged();
    }

    void UpdateSubgraphInputs()
    {
      ForceUpdatePorts();
      NotifyNodeChanged();
    }

    void UpdateSubgraphInputs(ExposedParameter _) => UpdateSubgraphInputs();

    void UpdateSubgraphOutputs()
    {
      _node.UpdateOutputTextures();
      UpdatePreviewUI();
      ForceUpdatePorts();
      NotifyNodeChanged();
    }

    void UpdateSubgraphOutputs(string _) => UpdateSubgraphOutputs();

    void UpdatePreviewUI()
    {
      var settings = _node.subgraph.outputNode.outputTextureSettings;

      _previewOutputField.choices = settings.Select(setting => setting.name).ToList();
      _node.previewOutputIndex = Mathf.Clamp(_node.previewOutputIndex, 0, settings.Count - 1);
      _previewOutputField.index = _node.previewOutputIndex;
    }

    bool ValidateSubgraph()
    {
      _node.ClearMessages();

      if (_node.subgraphTexture == null)
      {
        return false;
      }

      if (_node.subgraph == null)
      {
        _node.AddMessage($"Cannot find Mixture graph for texture: {_node.subgraphTexture.name}", NodeMessageType.Error);
        return false;
      }

      if (_node.subgraph == _node.graph)
      {
        _node.AddMessage($"Cannot execute graph recursively!", NodeMessageType.Error);
        return false;
      }

      return true;
    }
  }
}
