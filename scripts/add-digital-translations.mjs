// SPDX-License-Identifier: GPL-3.0-only
import fs from 'node:fs';
const file='src/Ntscrt.Windows/Translations.json';
const catalog=JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
const codes=['en','es','fr','pt-BR','it','ru','zh-Hans','ja','ko'];
const entries=[
 ['Digitalvideo-Format','Digital video format','Formato de vídeo digital','Format vidéo numérique','Formato de vídeo digital','Formato video digitale','Формат цифрового видео','数字视频格式','デジタルビデオ形式','디지털 비디오 형식'],
 ['Eigene Einstellungen','Custom settings','Ajustes personalizados','Réglages personnalisés','Configurações personalizadas','Impostazioni personalizzate','Свои настройки','自定义设置','カスタム設定','사용자 설정'],
 ['Digitalvideo aktiv','Enable digital video','Activar vídeo digital','Activer la vidéo numérique','Ativar vídeo digital','Attiva video digitale','Включить цифровое видео','启用数字视频','デジタルビデオを有効化','디지털 비디오 활성화'],
 ['Kamera- und Codec-Effekte aus NTSCRT von Finn McKenty. Details verwenden die Original-Parameternamen.','Camera and codec effects from NTSCRT by Finn McKenty. Details use the original parameter names.','Efectos de cámara y códec de NTSCRT de Finn McKenty. Los detalles usan los nombres originales de los parámetros.','Effets de caméra et de codec issus de NTSCRT de Finn McKenty. Les détails utilisent les noms de paramètres d’origine.','Efeitos de câmera e codec do NTSCRT de Finn McKenty. Os detalhes usam os nomes originais dos parâmetros.','Effetti di videocamera e codec da NTSCRT di Finn McKenty. I dettagli usano i nomi originali dei parametri.','Эффекты камеры и кодека из NTSCRT Финна Маккенти. В подробных настройках используются исходные имена параметров.','相机和编解码器效果来自 Finn McKenty 的 NTSCRT。详细设置使用原始参数名称。','Finn McKenty氏のNTSCRTによるカメラとコーデック効果。詳細では元のパラメーター名を使用します。','Finn McKenty의 NTSCRT 카메라 및 코덱 효과입니다. 세부 설정은 원래 매개변수 이름을 사용합니다.']
];
for(const [de,...values]of entries){catalog.translations[de]=values[0];for(let i=1;i<codes.length;i++)catalog.languages[codes[i]][de]=values[i];}
delete catalog.translations['Gesamtlook wählen'];
for(const language of Object.values(catalog.languages))delete language['Gesamtlook wählen'];
delete catalog.resources.DigitalLookLabel;
Object.assign(catalog.resources,{DigitalFormatLabel:entries[0][0],DigitalCustomLabel:entries[1][0],DigitalEnabledLabel:entries[2][0],DigitalHintLabel:entries[3][0]});
fs.writeFileSync(file,JSON.stringify(catalog,null,2)+'\n');
