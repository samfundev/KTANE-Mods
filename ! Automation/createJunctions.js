const fs = require("fs-extra");
const { promisify } = require("util");
const { join, resolve } = require("path");

const readdir = promisify(fs.readdir);
const stat = promisify(fs.stat);
const lstat = promisify(fs.lstat);
const symlink = promisify(fs.symlink);
const access = promisify(fs.access);

function catchify(promise) {
	return promise
		.then(resp => [null, resp])
		.catch(err => [err, null]);
}

// All the directories that don't need KM_Assets
const KM_Assetless = [
	"BetterCasePicker",
	"BombTimeExtender",
	"MissionMaker",
	"PacingExtender",
	"SoundpackMaker",
	"Tweaks"
]

const junctions = [
	"Assets\\Editor\\Scripts",
	"Assets\\Editor\\Steamworks.NET",
	"Assets\\Plugins",
	"Assets\\KM_Assets",
	"Assets\\TestHarness",
	"Assets\\Shared_Assets",
	"ProjectSettings",
];

process.chdir("..");

(async function() {
	let files = await readdir(".", undefined);
	for (let file of files) {
		if (file != "! Template Project" && (file.startsWith("! ") || file.startsWith("."))) continue;

		let stats = await stat(file);
		if (!stats.isDirectory()) continue;

		for (let junc of junctions) {
			let folder = join(file, junc);
			if (KM_Assetless.includes(file) && junc == "Assets\\KM_Assets") {
				fs.remove(folder);
				continue;
			}

			let [err, stats] = await catchify(lstat(folder));
			if (err == null) {
				if (stats.isSymbolicLink()) continue;
				else await fs.remove(folder);
			}

			await symlink(resolve(join("! Source Project", junc)), folder, "junction");
			console.log(`Created junction for "${folder}"`);
		}
	}
})();