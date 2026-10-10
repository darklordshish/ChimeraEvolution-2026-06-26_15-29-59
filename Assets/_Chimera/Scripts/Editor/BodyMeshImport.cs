using UnityEditor;

/// <summary>Импорт любой модели сбрасывает кэш мешей вида: переимпорт подменяет содержимое меша под прежним
/// идентификатором, и `BodyMesh` иначе ставил бы на тело геометрию прошлой версии файла — без единой ошибки.</summary>
class BodyMeshImport : AssetPostprocessor
{
    void OnPostprocessModel(UnityEngine.GameObject _) => BodyMesh.ResetCache();
}
