using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CriticalAngle.Editor
{
    [CustomEditor(typeof(ScalablePlayer))]
    public class ScalablePlayerEditor : UnityEditor.Editor
    {
        private bool statesFoldout;
        private ReorderableList parametersList;
        private ReorderableList statesList;
        private ReorderableList stateTransitionsList;
        private ReorderableList stateConditionsList;
        private string currentSelectedState;

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

                        var player = (ScalablePlayer) this.target;
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

                    var player = (ScalablePlayer) this.target;
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

            var player = (ScalablePlayer)this.target;

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
            player.ApplyVariables();

            this.serializedObject.ApplyModifiedProperties();
        }
    }
}