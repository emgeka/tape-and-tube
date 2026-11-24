// SPDX-License-Identifier: GPL-3.0-only
// Original parameter/format data: NTSCRT, MIT, Finn McKenty 2026.
import fs from 'node:fs';
const description=JSON.parse(fs.readFileSync(process.argv[2] ?? 'artifacts/core-prototype/windows-final-verified/description.json','utf8').replace(/^\uFEFF/,''));
const defaults=Object.fromEntries(description.digitalParameters.map(p=>[p.id,p.default]));
const catalog={schemaVersion:1,revision:description.sources.ntscrt,license:'MIT',copyright:'Copyright (c) 2026 Finn McKenty',source:'https://github.com/finnmckenty/NTSCRT',defaults,parameters:description.digitalParameters,formats:description.digitalFormats.map(f=>({...f,values:{...defaults,...f.values}}))};
fs.writeFileSync('src/Ntscrt.Windows/DigitalCatalog.json',JSON.stringify(catalog,null,2)+'\n');
