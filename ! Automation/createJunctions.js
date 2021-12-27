const fs = require("fs-extra");
const { join, relative, dirname, basename } = require("path");
const { existsSync } = require("fs")

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
	join("Assets", "Editor", "Scripts"),
	join("Assets", "Editor", "Steamworks.NET"),
	join("Assets", "Plugins"),
	join("Assets", "Scripts"),
	join("Assets", "KM_Assets"),
	join("Assets", "TestHarness"),
	join("Assets", "Shared_Assets"),
	join("ProjectSettings"),
	join("Manual", "css"),
	join("Manual", "js"),
	join("Manual", "font"),
	join("Manual", "img", "page-bg-noise-01.png"),
	join("Manual", "img", "page-bg-noise-02.png"),
	join("Manual", "img", "page-bg-noise-03.png"),
	join("Manual", "img", "page-bg-noise-04.png"),
	join("Manual", "img", "page-bg-noise-05.png"),
	join("Manual", "img", "page-bg-noise-06.png"),
	join("Manual", "img", "page-bg-noise-07.png"),
	join("Manual", "img", "web-background.jpg"),
	join(".gitignore"),
];

async function hardLinkDir(target, directory) {
	const newDir = join(directory, basename(target));
	if (!existsSync(newDir))
		await fs.mkdir(newDir);

	for (const entity of await fs.readdir(target, { withFileTypes: true })) {
		if (entity.isFile()) {
			const newFile = join(newDir, entity.name);
			if (!existsSync(newFile))
				await fs.link(join(target, entity.name), newFile)
		} else if (entity.isDirectory()) {
			await hardLinkDir(join(target, entity.name), newDir);
		}
	}
}

process.chdir("..");

(async function () {
	let files = await fs.readdir(".", undefined);
	for (let file of files) {
		if (file != "! Template Project" && (file.startsWith("! ") || file.startsWith(".") || file == "docs" || file == "build")) continue;

		let stats = await fs.stat(file);
		if (!stats.isDirectory()) continue;

		for (let junc of junctions) {
			let folder = join(file, junc);
			if (KM_Assetless.includes(file) && junc == join("Assets", "KM_Assets")) {
				fs.remove(folder);
				continue;
			}

			if (Shared_Assetsless.includes(file) && junc == join("Assets", "Shared_Assets")) {
				fs.remove(folder);
				continue;
			}

			fs.access(dirname(folder)).then(async () => {

				const isDir = (await fs.lstat(join("! Source Project", junc))).isDirectory();
				if (process.platform === "win32") {
					let [err, stats] = await catchify(fs.lstat(folder));
					if (err === null) {
						if (stats.isSymbolicLink()) return;
						else await fs.remove(folder);
					}

					const type = isDir ? "junction" : "file";
					await fs.symlink(relative(join(folder, ".."), join("! Source Project", junc)), folder, type)
						.catch(console.error)
						.then(() => console.log(`Created junction for "${folder}"`));
				} else if (process.platform === "linux") {
					// Git doesn't follow symlinks and Linux doesn't support directory hard links.
					// So, we have to recreate the directory structure and hard link any files.
					if (isDir) {
						hardLinkDir(join("! Source Project", junc), dirname(folder))
							.catch(console.error)
							.then(() => console.log(`Created junction for "${folder}"`));
					} else if (!existsSync(folder)) {
						fs.link(join("! Source Project", junc), folder)
							.catch(console.error)
							.then(() => console.log(`Created junction for "${folder}"`));
					}
				}
			}).catch(error => {
				if (!junc.startsWith("Manual")) console.error(error);
			});
		}
	}
})();