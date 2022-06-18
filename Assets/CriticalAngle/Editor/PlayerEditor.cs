using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CriticalAngle.ExpandablePlayer.Editor
{
    [CustomEditor(typeof(Player))]
    public class PlayerEditor : UnityEditor.Editor
    {
        private bool statesFoldout;
        private ReorderableList parametersList;
        private ReorderableList statesList;
        private ReorderableList stateTransitionsList;
        private ReorderableList stateConditionsList;
        private string currentSelectedState;

        private bool inputFoldout;
        private SerializedProperty inputActions;

        private void OnEnable()
        {
            this.parametersList =
                new ReorderableList(this.serializedObject, this.serializedObject.FindProperty("Parameters"))
                {
                    drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Parameters"),
                    drawElementCallback = (rect, index, active, focused) =>
                    {
                        var element = this.parametersList.serializedProperty.GetArrayElementAtIndex(index);
                        
                        EditorGUI.LabelField(new Rect(rect.x, rect.y + 2, 50, EditorGUIUtility.singleLineHeight), "Name");
                        EditorGUI.PropertyField(new Rect(rect.x + 50, rect.y + 2, 100, EditorGUIUtility.singleLineHeight),
                            element.FindPropertyRelative("Name"),
                            GUIContent.none);

                        var player = (Player) this.target;
                    },
                };

            this.statesList = new ReorderableList(this.serializedObject, this.serializedObject.FindProperty("States"))
            {
                drawElementCallback = this.DrawStatesListItems,
                drawHeaderCallback = this.DrawStatesHeader,
                onSelectCallback = this.OnStateSelected,
                onRemoveCallback = list =>
                {
                    this.stateTransitionsList = null;
                    this.stateConditionsList = null;
                    ReorderableList.defaultBehaviours.DoRemoveButton(list);
                }
            };

            this.inputActions = this.serializedObject.FindProperty("InputActions");
        }

        private void DrawStatesListItems(Rect rect, int index, bool isactive, bool isfocused)
        {
            var element = this.statesList.serializedProperty.GetArrayElementAtIndex(index);

            EditorGUI.LabelField(new Rect(rect.x, rect.y + 2, 50, EditorGUIUtility.singleLineHeight), "Name");
            EditorGUI.PropertyField(
                new Rect(rect.x + 50, rect.y + 2, 100, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative("Name"),
                GUIContent.none);
        }

        private void DrawStatesHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "States");
        }

        private void OnStateSelected(ReorderableList list)
        {
            this.stateConditionsList = null;
            
            var transitions = list.serializedProperty.GetArrayElementAtIndex(list.index)
                .FindPropertyRelative("Transitions");

            this.currentSelectedState = list.serializedProperty.GetArrayElementAtIndex(list.index)
                .FindPropertyRelative("Name")
                .stringValue;
            
            this.stateTransitionsList = new ReorderableList(this.serializedObject, transitions)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, this.currentSelectedState + " Transitions");
                },
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = this.stateTransitionsList.serializedProperty.GetArrayElementAtIndex(index);

                    var stateNames = new string[this.statesList.count];
                    for (var i = 0; i < this.statesList.count; i++)
                    {
                        stateNames[i] = this.statesList.serializedProperty.GetArrayElementAtIndex(i)
                            .FindPropertyRelative("Name").stringValue;
                    }

                    element.FindPropertyRelative("ToState").intValue = EditorGUI.Popup(
                        new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight),
                        "Next State", element.FindPropertyRelative("ToState").intValue, stateNames);
                },
                onSelectCallback = this.OnTransitionSelected,
                // ReSharper disable once VariableHidesOuterVariable
                onRemoveCallback = list =>
                {
                    this.stateConditionsList = null;
                    ReorderableList.defaultBehaviours.DoRemoveButton(list);
                }
            };
        }

        private void OnTransitionSelected(ReorderableList list)
        {
            var stateNames = new string[this.statesList.count];
            for (var i = 0; i < this.statesList.count; i++)
                stateNames[i] = this.statesList.serializedProperty.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("Name").stringValue;

            var conditions = list.serializedProperty.GetArrayElementAtIndex(list.index)
                .FindPropertyRelative("Conditions");
            this.stateConditionsList = new ReorderableList(this.serializedObject, conditions)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect,
                        this.currentSelectedState + "->" +
                        stateNames[list.serializedProperty.GetArrayElementAtIndex(list.index).FindPropertyRelative("ToState")
                            .intValue] + " Conditions");
                },
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = this.stateConditionsList.serializedProperty.GetArrayElementAtIndex(index);

                    var player = (Player) this.target;
                    var paramNames = new string[player.Parameters.Count];
                    for (var i = 0; i < player.Parameters.Count; i++)
                        paramNames[i] = player.Parameters[i].Name;
                    
                    element.FindPropertyRelative("Name").intValue = EditorGUI.Popup(
                        new Rect(rect.x, rect.y + 2, rect.width - 100, EditorGUIUtility.singleLineHeight),
                        "Parameter", element.FindPropertyRelative("Name").intValue, paramNames);
                    
                    EditorGUI.PropertyField(
                        new Rect(rect.x + rect.width - 100, rect.y + 2, 100, EditorGUIUtility.singleLineHeight),
                        element.FindPropertyRelative("Value"),
                        GUIContent.none);
                },
            };
        }

        public override void OnInspectorGUI()
        {
            EditorUtility.SetDirty(this.target);

            var player = (Player)this.target;

            if (GUILayout.Button("Setup References"))
                player.SetupReferences();

            this.serializedObject.Update();
            DrawPropertiesExcluding(this.serializedObject, "m_Script");

            this.statesFoldout = EditorGUILayout.Foldout(this.statesFoldout, "State Settings", true);

            if (this.statesFoldout)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.BeginVertical();
                GUILayout.Space(5);

                this.parametersList.DoLayoutList();
                
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                
                this.statesList.DoLayoutList();
                GUILayout.Space(5);
                this.stateTransitionsList?.DoLayoutList();
                GUILayout.Space(5);
                this.stateConditionsList?.DoLayoutList();
                GUILayout.Space(5);

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            
            GUILayout.Space(2);
            
            this.inputFoldout = EditorGUILayout.Foldout(this.inputFoldout, "Input Settings", true);

            if (this.inputFoldout)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.BeginVertical();
                GUILayout.Space(5);

                EditorGUILayout.PropertyField(this.inputActions, new GUIContent("Input Actions Asset",
                    "An asset generated by Unity's new Input System to keep track of the user's inputs."));

                if (player.InputActions != null)
                {
                    var mappings = player.InputActions.actionMaps.ToArray().Select(map => map.name).ToArray();
                    player.InputMapping = EditorGUILayout.Popup(new GUIContent("Mapping",
                        "Which mapping within the Input Actions Asset should we use? This will determine " +
                        "which input bindings we can select below."), player.InputMapping, mappings);

                    if (player.InputActions.actionMaps[player.InputMapping] != null)
                    {
                        GUILayout.Space(10);
                        
                        var bindings = player.InputActions.actionMaps[player.InputMapping].actions.ToArray();
                        var bindingNames = bindings.Select(binding => binding.name).ToArray();
                        player.MoveBinding = EditorGUILayout.Popup(new GUIContent("Move Binding",
                            "e.g., WASD, Left Joystick."), player.MoveBinding, bindingNames);
                        player.RunBinding = EditorGUILayout.Popup(new GUIContent("Run Binding",
                            "e.g., Left Shift."), player.RunBinding, bindingNames);
                        player.LookBinding = EditorGUILayout.Popup(new GUIContent("Look Binding",
                            "e.g., Mouse Movement Delta, Right Joystick."), player.LookBinding, bindingNames);
                        player.JumpBinding = EditorGUILayout.Popup(new GUIContent("Jump Binding",
                            "e.g., Spacebar, Gamepad A."), player.JumpBinding, bindingNames);
                        player.CrouchBinding = EditorGUILayout.Popup(new GUIContent("Crouch Binding",
                            "e.g., Left Control, Gamepad B."), player.CrouchBinding, bindingNames);
                    }
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            var newParameters = new List<string>();
            for (var i = 0; i < player.Parameters.Count; i++)
            {
                if (newParameters.Contains(player.Parameters[i].Name))
                    player.Parameters[i].Name = i.ToString();
                else
                    newParameters.Add(player.Parameters[i].Name);
            }
            
            var newStates = new List<string>();
            for (var i = 0; i < player.States.Count; i++)
            {
                if (newStates.Contains(player.States[i].Name))
                    player.States[i].Name = i.ToString();
                else
                    newStates.Add(player.States[i].Name);
            }

            player.HideComponents();

            if (!Application.isPlaying)
                player.ApplyVariables();

            this.serializedObject.ApplyModifiedProperties();
        }
    }
}