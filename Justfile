media:
	magick media/icon.svg -resize 128 -colors 8 PNG8:media/icon.png

docs *ARGS:
	mkdir -p docs/media
	rm -rf _site docs/api
	yq -o json < docs/docfx.yml > docs/docfx.json
	dotnet docfx metadata docs/docfx.json
	mv docs/api/Grist4NET/Grist4NET.yml docs/api/Grist4NET/index.yml
	ln -f README.md docs/index.md
	ln -f media/* docs/media
	dotnet docfx build docs/docfx.json {{ARGS}}

deploy BRANCH="preview": docs
	wrangler pages deploy --branch="{{BRANCH}}" _site
