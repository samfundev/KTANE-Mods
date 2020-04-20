const fs = require("fs-extra");
const { join, relative, dirname, extname } = require("path");

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
]

// All the directories that don't need Shared_Assets
const Shared_Assetsless = [
	"Tweaks",
];

const junctions = [
	"Assets\\Editor\\Scripts",
	"Assets\\Editor\\Steamworks.NET",
	"Assets\\Plugins",
	"Assets\\Scripts",
	"Assets\\KM_Assets",
	"Assets\\TestHarness",
	"Assets\\Shared_Assets",
	"ProjectSettings",
	"Manual\\css",
	"Manual\\js",
	"Manual\\font",
	"Manual\\img\\page-bg-noise-01.png",
	"Manual\\img\\page-bg-noise-02.png",
	"Manual\\img\\page-bg-noise-03.png",
	"Manual\\img\\page-bg-noise-04.png",
	"Manual\\img\\page-bg-noise-05.png",
	"Manual\\img\\page-bg-noise-06.png",
	"Manual\\img\\page-bg-noise-07.png",
	"Manual\\img\\web-background.jpg",
	".gitignore",
];

process.chdir("..");

(async function() {
	let files = await fs.readdir(".", undefined);
	for (let file of files) {
		if (file != "! Template Project" && (file.startsWith("! ") || file.startsWith("."))) continue;

		let stats = await fs.stat(file);
		if (!stats.isDirectory()) continue;

		for (let junc of junctions) {
			let folder = join(file, junc);
			if (KM_Assetless.includes(file) && junc == "Assets\\KM_Assets") {
				fs.remove(folder);
				continue;
			}

			if (Shared_Assetsless.includes(file) && junc == "Assets\\Shared_Assets") {
				fs.remove(folder);
				continue;
			}

			fs.access(dirname(folder)).then(async () => {
				let [err, stats] = await catchify(fs.lstat(folder));
				if (err == null) {
					if (stats.isSymbolicLink()) return;
					else await fs.remove(folder);
				}
	
				await fs.symlink(relative(join(folder, ".."), join("! Source Project", junc)), folder, (await fs.lstat(join("! Source Project", junc))).isDirectory() ? "junction" : "file")
					.catch(console.error)
					.then(() => console.log(`Created junction for "${folder}"`));
			}).catch(error => {
				if (!junc.startsWith("Manual\\")) console.error(error);
			});
		}
	}
})();